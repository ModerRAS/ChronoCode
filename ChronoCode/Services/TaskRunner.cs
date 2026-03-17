using ChronoCode.Models;

namespace ChronoCode.Services;

public interface ITaskRunner
{
    Task<TaskExecution> ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default);
}

public class TaskRunner : ITaskRunner
{
    private readonly IOpencodeClient _opencodeClient;
    private readonly IGitService _gitService;
    private readonly IExecutionRepository _executionRepository;
    private readonly IOpencodeServerManager _serverManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskRunner> _logger;

    private string WorkspaceBasePath => _configuration["TaskRunner:WorkspaceBasePath"] ?? "/workspaces";

    public TaskRunner(
        IOpencodeClient opencodeClient,
        IGitService gitService,
        IExecutionRepository executionRepository,
        IOpencodeServerManager serverManager,
        IConfiguration configuration,
        ILogger<TaskRunner> logger)
    {
        _opencodeClient = opencodeClient;
        _gitService = gitService;
        _executionRepository = executionRepository;
        _serverManager = serverManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TaskExecution> ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        var execution = await _executionRepository.CreateAsync(task.Id);
        var workspacePath = Path.Combine(WorkspaceBasePath, task.Id.ToString(), DateTime.UtcNow.ToString("yyyyMMddHHmmss"));

        await LogAsync(execution.Id, "Info", $"Starting task execution: {task.Name}");
        await LogAsync(execution.Id, "Info", $"Workspace: {workspacePath}");

        try
        {
            await EnsureServerRunningAsync(cancellationToken);

            var branchName = task.BranchStrategy == BranchStrategy.New
                ? $"chronocode/{DateTime.UtcNow:yyyyMMddHHmmss}"
                : $"chronocode/main";

            await CloneAndSetupRepoAsync(task, workspacePath, branchName, cancellationToken);

            await LogAsync(execution.Id, "Info", "Starting PLAN phase");
            var planResult = await ExecutePlanPhaseAsync(task, workspacePath, cancellationToken);

            if (task.RequirePlanReview)
            {
                await LogAsync(execution.Id, "Info", "Plan review required - simulating review confirmation");
                await Task.Delay(2000, cancellationToken);
            }

            await LogAsync(execution.Id, "Info", "Starting EXECUTE phase");
            await ExecutePlanPhaseAsync(task, workspacePath, cancellationToken);

            var changedFiles = await _gitService.GetChangedFilesAsync(workspacePath);
            await LogAsync(execution.Id, "Info", $"Changed {changedFiles.Count} files");

            if (changedFiles.Count > task.MaxFileChanges)
            {
                throw new Exception($"Too many files changed: {changedFiles.Count} (max: {task.MaxFileChanges})");
            }

            var commitSha = await _gitService.CommitChangesAsync(workspacePath, $"AI: {task.Name}");
            await LogAsync(execution.Id, "Info", $"Committed: {commitSha}");

            await _gitService.PushChangesAsync(workspacePath);
            await LogAsync(execution.Id, "Info", "Changes pushed");

            var prUrl = await _gitService.CreatePullRequestAsync(workspacePath, branchName, task.BaseBranch, task.Name, task.Prompt);
            await LogAsync(execution.Id, "Info", $"Pull request created: {prUrl}");

            execution.Status = Models.TaskStatus.Completed;
            execution.BranchName = branchName;
            execution.CommitSha = commitSha;
            execution.PrUrl = prUrl;
            execution.FilesChanged = changedFiles.Count;
            execution.CompletedAt = DateTime.UtcNow;

            await _executionRepository.UpdateAsync(execution);
            await LogAsync(execution.Id, "Info", "Task completed successfully");

            return execution;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed: {TaskId}", task.Id);
            await LogAsync(execution.Id, "Error", $"Task failed: {ex.Message}", ex.StackTrace);

            execution.Status = Models.TaskStatus.Failed;
            execution.ErrorMessage = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;
            await _executionRepository.UpdateAsync(execution);

            throw;
        }
    }

    private async Task EnsureServerRunningAsync(CancellationToken cancellationToken)
    {
        if (!_serverManager.IsServerRunning)
        {
            await LogAsync(Guid.Empty, "Info", "Starting opencode server...");
            await _serverManager.StartServerAsync(cancellationToken);
            await _serverManager.WaitForServerReadyAsync(TimeSpan.FromSeconds(30));
        }
    }

    private async Task CloneAndSetupRepoAsync(ScheduledTask task, string workspacePath, string branchName, CancellationToken cancellationToken)
    {
        await LogAsync(Guid.Empty, "Info", $"Cloning {task.RepositoryUrl}");
        await _gitService.CloneRepositoryAsync(task.RepositoryUrl, workspacePath);

        await LogAsync(Guid.Empty, "Info", $"Creating branch: {branchName}");
        await _gitService.CreateBranchAsync(workspacePath, branchName, task.BaseBranch);
        await _gitService.CheckoutBranchAsync(workspacePath, branchName);
    }

    private async Task<string> ExecutePlanPhaseAsync(ScheduledTask task, string workspacePath, CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(task);

        await _opencodeClient.SendPromptWithStreamingAsync(
            await _opencodeClient.CreateSessionAsync(workspacePath, cancellationToken),
            prompt,
            workspacePath,
            async (chunk) =>
            {
                await LogAsync(Guid.Empty, "Debug", $"AI: {chunk}");
            },
            cancellationToken);

        return "Plan executed";
    }

    private string BuildPrompt(ScheduledTask task)
    {
        return $@"
You are an AI coding assistant. Execute the following task:

{task.Prompt}

CONSTRAINTS:
- Maximum {task.MaxFileChanges} files can be modified
- Maximum runtime: {task.MaxRuntimeSeconds} seconds
- Always create proper commit messages

ALLOWED ACTIONS:
- Read files
- Modify code
- Add new files
- Create commits

FORBIDDEN ACTIONS:
- Force push
- Delete branches
- Delete more than 10 files at once
- Modify CI/CD configurations
- Change permissions

Please analyze the codebase and implement the requested changes. Start by exploring the project structure, then make your changes.
";
    }

    private async Task LogAsync(Guid executionId, string level, string message, string? details = null)
    {
        if (executionId != Guid.Empty)
        {
            await _executionRepository.AddLogAsync(executionId, level, message, details);
        }
        _logger.Log(level switch
        {
            "Error" => LogLevel.Error,
            "Warning" => LogLevel.Warning,
            "Debug" => LogLevel.Debug,
            _ => LogLevel.Information
        }, "{Message}", message);
    }
}
