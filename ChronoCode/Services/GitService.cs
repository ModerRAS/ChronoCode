using System.Text;
using System.Text.Json;
using LibGit2Sharp;

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
    private readonly ILogger<GitService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _githubToken;

    public GitService(ILogger<GitService> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _githubToken = configuration["GitHub:Token"];
    }

    public async Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath)
    {
        _logger.LogInformation("Cloning repository {RepoUrl} to {Path}", repoUrl, workspacePath);

        return await Task.Run(() =>
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, true);
            }

            var repoPath = Repository.Clone(repoUrl, workspacePath);
            _logger.LogInformation("Repository cloned to {Path}", repoPath);
            return repoPath;
        });
    }

    public async Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch)
    {
        _logger.LogInformation("Creating branch {Branch} from {Base} in {Path}", branchName, baseBranch, repoPath);

        return await Task.Run(() =>
        {
            using var repo = new Repository(repoPath);

            var baseCommit = repo.Branches[baseBranch]?.Tip;
            if (baseCommit == null)
                throw new Exception($"Base branch {baseBranch} not found");

            repo.CreateBranch(branchName, baseCommit);
            var branch = repo.Branches[branchName];
            if (branch != null)
            {
                repo.Branches.Update(branch, b => b.TrackedBranch = baseBranch);
            }

            _logger.LogInformation("Created branch {Branch}", branchName);
            return branchName;
        });
    }

    public async Task CheckoutBranchAsync(string repoPath, string branchName)
    {
        _logger.LogInformation("Checking out branch {Branch} in {Path}", branchName, repoPath);

        await Task.Run(() =>
        {
            using var repo = new Repository(repoPath);
            var branch = repo.Branches[branchName];

            if (branch == null)
                throw new Exception($"Branch {branchName} not found");

            Commands.Checkout(repo, branch);
            _logger.LogInformation("Checked out branch {Branch}", branchName);
        });
    }

    public async Task<string> CommitChangesAsync(string repoPath, string message)
    {
        _logger.LogInformation("Committing changes in {Path}", repoPath);

        return await Task.Run(() =>
        {
            using var repo = new Repository(repoPath);

            Commands.Stage(repo, "*");

            if (!repo.RetrieveStatus().IsDirty)
            {
                _logger.LogWarning("No changes to commit");
                return string.Empty;
            }

            var signature = new Signature("ChronoCode Bot", "bot@chronocode.local", DateTimeOffset.Now);
            var commit = repo.Commit(message, signature, signature);

            _logger.LogInformation("Committed changes: {CommitSha}", commit.Sha);
            return commit.Sha;
        });
    }

    public async Task PushChangesAsync(string repoPath, string remoteName = "origin")
    {
        _logger.LogInformation("Pushing changes to {Remote} from {Path}", remoteName, repoPath);

        await Task.Run(() =>
        {
            using var repo = new Repository(repoPath);

            var remote = repo.Network.Remotes[remoteName];
            if (remote == null)
                throw new Exception($"Remote {remoteName} not found");

            var branch = repo.Head;
            var refSpec = $"refs/heads/{branch.FriendlyName}:refs/heads/{branch.FriendlyName}";

            var pushOptions = new PushOptions();
            
            if (!string.IsNullOrEmpty(_githubToken))
            {
                var credentials = new UsernamePasswordCredentials
                {
                    Username = "x-access-token",
                    Password = _githubToken
                };
                
                pushOptions.CredentialsProvider = (_, _, _) => credentials;
            }

            repo.Network.Push(remote, refSpec, pushOptions);
            _logger.LogInformation("Changes pushed successfully");
        });
    }

    public async Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
    {
        _logger.LogInformation("Creating pull request for {Branch} -> {Base}", branchName, baseBranch);

        if (string.IsNullOrEmpty(_githubToken))
        {
            var (fallbackOwner, fallbackRepo) = ExtractOwnerAndRepoParts(repoPath);
            var fallbackUrl = $"https://github.com/{fallbackOwner}/{fallbackRepo}/pull/new/{branchName}";
            _logger.LogWarning("GitHub token not configured. Returning fallback URL: {Url}", fallbackUrl);
            return fallbackUrl;
        }

        var (owner, repo) = ExtractOwnerAndRepoParts(repoPath);
        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/pulls";
        
        var payload = new
        {
            title,
            body,
            @base = baseBranch,
            head = branchName
        };

        using var client = _httpClientFactory.CreateClient("GitHub");
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_githubToken}");
        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        client.DefaultRequestHeaders.Add("User-Agent", "ChronoCode");

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

        return await Task.Run(() =>
        {
            using var repo = new Repository(repoPath);
            var status = repo.RetrieveStatus(new StatusOptions());

            return status
                .Where(s => s.State != FileStatus.Ignored)
                .Select(s => new GitFileStatus
                {
                    Path = s.FilePath,
                    Status = s.State.ToString()
                })
                .ToList();
        });
    }

    private (string owner, string repo) ExtractOwnerAndRepoParts(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var remote = repo.Network.Remotes["origin"];
        if (remote == null) return ("owner", "repo");

        var url = remote.Url;
        if (url.EndsWith(".git"))
            url = url[..^4];

        if (url.Contains("github.com/"))
        {
            var repoPathStr = url.Split("github.com/").Last();
            var segments = repoPathStr.Split('/');
            if (segments.Length >= 2)
                return (segments[0], segments[1]);
        }

        return ("owner", "repo");
    }
}

public class GitFileStatus
{
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
