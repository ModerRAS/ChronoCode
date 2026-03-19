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

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
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

            repo.Network.Push(repo.Head, new PushOptions());
            _logger.LogInformation("Changes pushed successfully");
        });
    }

    public async Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
    {
        _logger.LogInformation("Creating pull request for {Branch} -> {Base}", branchName, baseBranch);

        return await Task.Run(() =>
        {
            var prUrl = $"https://github.com/{ExtractOwnerAndRepo(repoPath)}/pull/new/{branchName}";
            _logger.LogInformation("GitHub API not integrated - returning mock PR URL: {Url}", prUrl);
            return prUrl;
        });
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

    private string ExtractOwnerAndRepo(string repoPath)
    {
        using var repo = new Repository(repoPath);
        var remote = repo.Network.Remotes["origin"];
        if (remote == null) return "owner/repo";

        var url = remote.Url;
        if (url.EndsWith(".git"))
            url = url[..^4];

        if (url.Contains("github.com/"))
            return url.Split("github.com/").Last();

        return "owner/repo";
    }
}

public class GitFileStatus
{
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
