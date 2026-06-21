using ChronoCode.Models;

namespace ChronoCode.Services;

public interface ITaskRunner
{
    Task<TaskExecution> ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default);
}

public class TaskRunner : ITaskRunner
{
    private readonly IAgentRuntime _agentRuntime;
    private readonly IGitService _gitService;
    private readonly IExecutionRepository _executionRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaskRunner> _logger;

    private string WorkspaceBasePath => _configuration["TaskRunner:WorkspaceBasePath"] ?? "/workspaces";

    public TaskRunner(
        IAgentRuntime agentRuntime,
        IGitService gitService,
        IExecutionRepository executionRepository,
        IConfiguration configuration,
        ILogger<TaskRunner> logger)
    {
        _agentRuntime = agentRuntime;
        _gitService = gitService;
        _executionRepository = executionRepository;
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
            await EnsureRuntimeReadyAsync(execution.Id, cancellationToken);

            var branchName = task.BranchStrategy == BranchStrategy.New
                ? $"chronocode/{DateTime.UtcNow:yyyyMMddHHmmss}"
                : $"chronocode/main";

            await CloneAndSetupRepoAsync(execution.Id, task, workspacePath, branchName, cancellationToken);

            var session = await _agentRuntime.EnsureExecutionSessionAsync(
                execution.Id,
                workspacePath,
                chunk => LogAsync(execution.Id, "Debug", chunk),
                cancellationToken: cancellationToken);

            await _executionRepository.UpdateSessionAsync(execution.Id, session);

            await LogAsync(
                execution.Id,
                "Info",
                $"Using {session.Backend} session",
                session.SessionFile == null
                    ? session.SessionId
                    : $"sessionId={session.SessionId}; sessionFile={session.SessionFile}");

            await ExecuteTaskPromptAsync(execution.Id, task, workspacePath, "PLAN", cancellationToken);

            if (task.RequirePlanReview)
            {
                await LogAsync(execution.Id, "Info", "Plan review required - simulating review confirmation");
                await Task.Delay(2000, cancellationToken);
            }

            await ExecuteTaskPromptAsync(execution.Id, task, workspacePath, "EXECUTE", cancellationToken);

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

    private async Task EnsureRuntimeReadyAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var status = _agentRuntime.GetStatus();
        if (!status.IsReady)
        {
            await LogAsync(executionId, "Info", $"Starting {status.Backend} runtime...");
        }

        await _agentRuntime.EnsureReadyAsync(cancellationToken);

        status = _agentRuntime.GetStatus();
        await LogAsync(
            executionId,
            "Info",
            $"{status.Backend} runtime ready",
            status.Endpoint == null ? null : $"Endpoint: {status.Endpoint}");
    }

    private async Task CloneAndSetupRepoAsync(Guid executionId, ScheduledTask task, string workspacePath, string branchName, CancellationToken cancellationToken)
    {
        await LogAsync(executionId, "Info", $"Cloning {task.RepositoryUrl}");
        await _gitService.CloneRepositoryAsync(task.RepositoryUrl, workspacePath);

        await LogAsync(executionId, "Info", $"Creating branch: {branchName}");
        await _gitService.CreateBranchAsync(workspacePath, branchName, task.BaseBranch);
        await _gitService.CheckoutBranchAsync(workspacePath, branchName);
    }

    private async Task ExecuteTaskPromptAsync(
        Guid executionId,
        ScheduledTask task,
        string workspacePath,
        string phase,
        CancellationToken cancellationToken)
    {
        await LogAsync(executionId, "Info", $"Starting {phase} phase");

        var prompt = BuildPrompt(task, phase);

        await _agentRuntime.SendMessageAsync(
            executionId,
            workspacePath,
            prompt,
            AgentMessageMode.Prompt,
            chunk => LogAsync(executionId, "Debug", chunk),
            cancellationToken);
    }

    private string BuildPrompt(ScheduledTask task, string phase)
    {
        var phaseInstruction = phase == "PLAN"
            ? "First inspect the repository and produce a concrete plan, then begin implementing if the plan is straightforward."
            : "Continue from your analysis and implement the requested changes completely. If the task is already complete, make no unnecessary edits.";

        return $@"
You are an AI coding assistant running inside a scheduled task executor.

TASK:
{task.Prompt}

CURRENT PHASE:
{phase}

CONSTRAINTS:
- Maximum {task.MaxFileChanges} files can be modified
- Maximum runtime: {task.MaxRuntimeSeconds} seconds
- Always create proper commit messages when changes are made
- Prefer the smallest correct change
- Do not force push
- Do not delete branches
- Do not delete more than 10 files at once
- Do not modify CI/CD configurations
- Do not change permissions

WORKFLOW:
- Explore the project structure first
- Respect repository instructions such as AGENTS.md when present
- Make changes directly in the checked out workspace
- Leave the repository clean enough for commit and PR creation

PHASE INSTRUCTION:
{phaseInstruction}
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
