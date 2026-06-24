using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ChronoCode.Services;

public interface IGitService
{
    Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath);
    Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch);
    Task CheckoutBranchAsync(string repoPath, string branchName);
    Task<string> CommitChangesAsync(string repoPath, string message);
    Task PushChangesAsync(string repoPath, string remoteName = "origin");
    Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body);
    Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath);
}

public class GitService : IGitService
{
    private static readonly TimeSpan CloneTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PushTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CommitTimeout = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan ShortCommandTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<GitService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _githubToken;

    private sealed record GitCommandResult(int ExitCode, string StdOut, string StdErr);

    public GitService(ILogger<GitService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _githubToken = configuration["GitHub:Token"];
    }

    public async Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath)
    {
        _logger.LogInformation("Cloning repository {RepoUrl} to {Path}", repoUrl, workspacePath);

        if (Directory.Exists(workspacePath))
        {
            Directory.Delete(workspacePath, true);
        }

        await RunGitAsync(null, CloneTimeout, ["clone", repoUrl, workspacePath]);
        _logger.LogInformation("Repository cloned to {Path}", workspacePath);
        return workspacePath;
    }

    public async Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch)
    {
        _logger.LogInformation("Creating branch {Branch} from {Base} in {Path}", branchName, baseBranch, repoPath);

        try
        {
            await RunGitAsync(repoPath, ShortCommandTimeout, ["rev-parse", "--verify", $"origin/{baseBranch}"]);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("git rev-parse failed with exit code ", StringComparison.Ordinal))
        {
            throw new Exception($"Base branch {baseBranch} not found");
        }

        await RunGitAsync(repoPath, ShortCommandTimeout, ["branch", branchName, $"origin/{baseBranch}"]);
        _logger.LogInformation("Created branch {Branch}", branchName);
        return branchName;
    }

    public async Task CheckoutBranchAsync(string repoPath, string branchName)
    {
        _logger.LogInformation("Checking out branch {Branch} in {Path}", branchName, repoPath);

        await RunGitAsync(repoPath, ShortCommandTimeout, ["checkout", branchName]);
        _logger.LogInformation("Checked out branch {Branch}", branchName);
    }

    public async Task<string> CommitChangesAsync(string repoPath, string message)
    {
        _logger.LogInformation("Committing changes in {Path}", repoPath);

        await RunGitAsync(repoPath, ShortCommandTimeout, ["add", "-A"]);

        var status = await RunGitAsync(repoPath, ShortCommandTimeout, ["status", "--porcelain"]);
        if (string.IsNullOrWhiteSpace(status.StdOut))
        {
            _logger.LogWarning("No changes to commit");
            return string.Empty;
        }

        await RunGitAsync(
            repoPath,
            CommitTimeout,
            ["-c", "user.name=ChronoCode Bot", "-c", "user.email=bot@chronocode.local", "commit", "-m", message]);

        var head = await RunGitAsync(repoPath, ShortCommandTimeout, ["rev-parse", "HEAD"]);
        var commitSha = head.StdOut.Trim();
        _logger.LogInformation("Committed changes: {CommitSha}", commitSha);
        return commitSha;
    }

    public async Task PushChangesAsync(string repoPath, string remoteName = "origin")
    {
        _logger.LogInformation("Pushing changes to {Remote} from {Path}", remoteName, repoPath);

        var branchResult = await RunGitAsync(repoPath, ShortCommandTimeout, ["rev-parse", "--abbrev-ref", "HEAD"]);
        var branchName = branchResult.StdOut.Trim();
        var remoteUrlResult = await RunGitAsync(repoPath, ShortCommandTimeout, ["remote", "get-url", remoteName]);
        var remoteUrl = remoteUrlResult.StdOut.Trim();

        Dictionary<string, string?>? environment = null;
        if (!string.IsNullOrWhiteSpace(_githubToken)
            && (remoteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            var tokenBytes = Encoding.UTF8.GetBytes($"x-access-token:{_githubToken}");
            environment = new Dictionary<string, string?>
            {
                ["GIT_CONFIG_COUNT"] = "1",
                ["GIT_CONFIG_KEY_0"] = "http.extraheader",
                ["GIT_CONFIG_VALUE_0"] = $"AUTHORIZATION: basic {Convert.ToBase64String(tokenBytes)}"
            };
        }

        await RunGitAsync(repoPath, PushTimeout, ["push", remoteName, $"HEAD:refs/heads/{branchName}"], environment);
        _logger.LogInformation("Changes pushed successfully");
    }

    public async Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
    {
        _logger.LogInformation("Creating pull request for {Branch} -> {Base}", branchName, baseBranch);

        if (string.IsNullOrEmpty(_githubToken))
        {
            throw new InvalidOperationException("GitHub:Token is required to create pull requests.");
        }

        var (owner, repo) = await ExtractOwnerAndRepoPartsAsync(repoPath);
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls";

        var payload = new
        {
            title,
            body,
            @base = baseBranch,
            head = branchName
        };

        using var client = _httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _githubToken);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(apiUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to create PR: {error}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var prUrl = doc.RootElement.GetProperty("html_url").GetString() ?? $"https://github.com/{owner}/{repo}/pull/new/{branchName}";

        _logger.LogInformation("Pull request created: {PrUrl}", prUrl);
        return prUrl;
    }

    public async Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath)
    {
        _logger.LogInformation("Getting changed files in {Path}", repoPath);

        var result = await RunGitAsync(repoPath, ShortCommandTimeout, ["status", "--porcelain"]);
        return result.StdOut
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Length >= 4)
            .Select(line => new GitFileStatus
            {
                Status = line.Substring(0, 2),
                Path = line.Substring(3)
            })
            .ToList();
    }

    private async Task<(string owner, string repo)> ExtractOwnerAndRepoPartsAsync(string repoPath)
    {
        var remoteResult = await RunGitAsync(repoPath, ShortCommandTimeout, ["remote", "get-url", "origin"]);
        var remoteUrl = remoteResult.StdOut.Trim();

        string? githubPath = null;
        const string httpsPrefix = "https://github.com/";
        const string sshPrefix = "git@github.com:";

        if (remoteUrl.StartsWith(httpsPrefix, StringComparison.OrdinalIgnoreCase)
            && remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            githubPath = remoteUrl[httpsPrefix.Length..^4];
        }
        else if (remoteUrl.StartsWith(sshPrefix, StringComparison.OrdinalIgnoreCase)
            && remoteUrl.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            githubPath = remoteUrl[sshPrefix.Length..^4];
        }

        if (string.IsNullOrWhiteSpace(githubPath))
        {
            throw new InvalidOperationException($"Unsupported git remote format: {remoteUrl}");
        }

        var segments = githubPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            throw new InvalidOperationException($"Unable to parse GitHub owner and repo from remote: {remoteUrl}");
        }

        return (segments[0], segments[1]);
    }

    private async Task<GitCommandResult> RunGitAsync(string? workingDirectory, TimeSpan timeout, IReadOnlyList<string> arguments, IDictionary<string, string?>? extraEnvironment = null)
    {
        if (arguments.Count == 0)
        {
            throw new ArgumentException("At least one git argument is required.", nameof(arguments));
        }

        var gitExecutable = OperatingSystem.IsWindows() ? "git.exe" : "git";
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = gitExecutable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            process.StartInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        if (extraEnvironment != null)
        {
            foreach (var kvp in extraEnvironment)
            {
                process.StartInfo.Environment[kvp.Key] = kvp.Value;
            }
        }

        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Unable to start git executable '{gitExecutable}'. Ensure Git is installed and available on PATH.", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var timeoutCts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }

            try
            {
                await process.WaitForExitAsync();
            }
            catch
            {
            }

            await Task.WhenAll(stdoutTask, stderrTask);
            throw new TimeoutException($"git {arguments[0]} timed out after {(int)timeout.TotalSeconds}s");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var result = new GitCommandResult(process.ExitCode, stdout, stderr);

        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StdErr)
                ? result.StdOut.Trim()
                : result.StdErr.Trim();
            throw new InvalidOperationException($"git {arguments[0]} failed with exit code {result.ExitCode}: {message}");
        }

        return result;
    }
}

public class GitFileStatus
{
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
