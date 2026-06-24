using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using ChronoCode.Services.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Additional NodeExecutor tests: PrepareWorkspace, CommitChanges edge cases,
/// CreatePullRequest template rendering, ApprovalGate custom message.
/// Also: AIStructuredResponse additional ToCreateTaskDto mappings.
/// </summary>
public class NodeExecutorAdditionalTests
{
    private static ScheduledTask MakeTask(int maxFileChanges = 50) => new()
    {
        Id = Guid.NewGuid(),
        Name = "test-task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        MaxFileChanges = maxFileChanges,
        WorkflowDefinitionJson = "{}",
        NodeFailurePolicyJson = "{}",
        CreatedAt = DateTime.UtcNow,
        LastStatus = TaskStatus.Pending
    };

    private static TaskExecution MakeRun() => new()
    {
        Id = Guid.NewGuid(),
        TaskId = Guid.NewGuid(),
        Status = TaskStatus.Running,
        StartedAt = DateTime.UtcNow,
        WorkflowSnapshotJson = "{}"
    };

    private static WorkflowContext CtxWithWorkspace()
    {
        var ctx = new WorkflowContext();
        ctx.Root["task"] = new JsonObject();
        ctx.Root["run"] = new JsonObject();
        ctx.Run["workspacePath"] = "/tmp/fake-workspace";
        ctx.Run["branchName"] = "chronocode/test";
        ctx.Task["name"] = "test-task";
        return ctx;
    }

    private static WorkflowNodeExecution MakeNode(string type = "commit_changes") => new()
    {
        Id = Guid.NewGuid(),
        ExecutionId = Guid.NewGuid(),
        NodeId = "node1",
        NodeType = type,
        ScopeKey = "root",
        Attempt = 0,
        Status = WorkflowNodeStatus.Running,
        StartedAt = DateTime.UtcNow
    };

    private static InMemoryExecutionRepository MakeExecRepo() =>
        new(NullLogger<InMemoryExecutionRepository>.Instance);

    private sealed class RecordingGitService : IGitService
    {
        public List<GitFileStatus> ChangedFiles { get; set; } = new();
        public string? LastCommitMessage { get; private set; }
        public string? LastPRTitle { get; private set; }
        public string? LastPRBody { get; private set; }
        public int PushCalls { get; private set; }

        public Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath) =>
            Task.FromResult(workspacePath);
        public Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch) =>
            Task.FromResult(branchName);
        public Task CheckoutBranchAsync(string repoPath, string branchName) => Task.CompletedTask;
        public Task<string> CommitChangesAsync(string repoPath, string message)
        { LastCommitMessage = message; return Task.FromResult("abc123"); }
        public Task PushChangesAsync(string repoPath, string remoteName = "origin")
        { PushCalls++; return Task.CompletedTask; }
        public Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
        { LastPRTitle = title; LastPRBody = body; return Task.FromResult("https://github.com/test/repo/pull/1"); }
        public Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath) => Task.FromResult(ChangedFiles);
    }

    // ---- PrepareWorkspaceNodeExecutor ----

    [Fact]
    public async Task PrepareWorkspace_ExecutesSuccessfully()
    {
        var prep = new FakeWorkspacePreparationService();
        var executor = new PrepareWorkspaceNodeExecutor(prep);
        var node = MakeNode("prepare_workspace");
        var def = new PrepareWorkspaceWorkflowNode { NodeId = "node1", Name = "Prepare", NextNodeId = "end" };
        var task = MakeTask();
        var run = MakeRun();
        var ctx = new WorkflowContext();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, CancellationToken.None);

        Assert.Equal("end", result.NextNodeId);
        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
    }

    // ---- CommitChanges: no changes = skip commit ----

    [Fact]
    public async Task CommitChanges_NoChanges_StillCompletesSuccessfully()
    {
        var git = new RecordingGitService { ChangedFiles = new() };
        var executor = new CommitChangesNodeExecutor(git, MakeExecRepo());
        var node = MakeNode("commit_changes");
        var def = new CommitChangesWorkflowNode { NodeId = "node1", Name = "Commit", NextNodeId = "end" };
        var ctx = CtxWithWorkspace();
        var task = MakeTask();
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
    }

    // ---- CommitChanges: renders commit message template ----

    [Fact]
    public async Task CommitChanges_RendersCommitMessage_WithTaskName()
    {
        var git = new RecordingGitService
        {
            ChangedFiles = new() { new() { Path = "src/main.ts", Status = "M" } }
        };
        var executor = new CommitChangesNodeExecutor(git, MakeExecRepo());
        var node = MakeNode("commit_changes");
        var def = new CommitChangesWorkflowNode
        {
            NodeId = "node1", Name = "Commit",
            CommitMessageTemplate = "AI: {{$.task.name}}",
            NextNodeId = "end"
        };
        var ctx = CtxWithWorkspace();
        var task = MakeTask();
        var run = MakeRun();

        await executor.ExecuteAsync(node, def, ctx, run, task, CancellationToken.None);

        Assert.Contains("test-task", git.LastCommitMessage);
    }

    // ---- CreatePullRequest: renders title and body ----

    [Fact]
    public async Task CreatePullRequest_RendersTitleAndBody_Templates()
    {
        var git = new RecordingGitService();
        var executor = new CreatePullRequestNodeExecutor(git, MakeExecRepo());
        var node = MakeNode("create_pull_request");
        var def = new CreatePullRequestWorkflowNode
        {
            NodeId = "node1", Name = "PR",
            TitleTemplate = "AI: {{$.task.name}}",
            BodyTemplate = "Automated changes for {{$.task.name}}",
            NextNodeId = "end"
        };
        var ctx = CtxWithWorkspace();
        var task = MakeTask();
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, CancellationToken.None);

        Assert.False(result.Failed);
        Assert.Contains("test-task", git.LastPRTitle);
        Assert.Contains("test-task", git.LastPRBody);
    }

    // ---- ApprovalGate: custom message ----

    [Fact]
    public async Task ApprovalGate_CustomMessage_PausesWithMessage()
    {
        var executor = new ApprovalGateNodeExecutor(MakeExecRepo());
        var node = MakeNode("approval_gate");
        var def = new ApprovalGateWorkflowNode
        {
            NodeId = "node1", Name = "Gate",
            Message = "Please review the AI changes before proceeding.",
            NextNodeId = "end"
        };
        var ctx = CtxWithWorkspace();
        var task = MakeTask();
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, CancellationToken.None);

        Assert.True(result.Paused);
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, node.Status);
    }

    // ---- AIStructuredResponse: ToCreateTaskDto branch strategy mappings ----

    [Fact]
    public void AITaskDto_ToCreateTaskDto_NewBranchStrategy()
    {
        var dto = new Models.AI.AITaskDto
        {
            Name = "Task",
            Cron = "0 0 * * *",
            Repository = "https://github.com/x/y",
            BranchStrategy = "new"
        };

        Assert.Equal(Models.BranchStrategy.New, dto.ToCreateTaskDto().BranchStrategy);
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_ReuseBranchStrategy()
    {
        var dto = new Models.AI.AITaskDto
        {
            Name = "Task",
            Cron = "0 0 * * *",
            Repository = "https://github.com/x/y",
            BranchStrategy = "reuse"
        };

        Assert.Equal(Models.BranchStrategy.Reuse, dto.ToCreateTaskDto().BranchStrategy);
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_UnknownStrategy_DefaultsToNew()
    {
        var dto = new Models.AI.AITaskDto
        {
            Name = "Task",
            Cron = "0 0 * * *",
            Repository = "https://github.com/x/y",
            BranchStrategy = "something_else"
        };

        Assert.Equal(Models.BranchStrategy.New, dto.ToCreateTaskDto().BranchStrategy);
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_DefaultsNodeFailurePolicy()
    {
        var dto = new Models.AI.AITaskDto
        {
            Name = "Task",
            Cron = "0 0 * * *",
            Repository = "https://github.com/x/y"
        };

        Assert.False(string.IsNullOrWhiteSpace(dto.ToCreateTaskDto().NodeFailurePolicyJson));
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_PreservesNodeFailurePolicy()
    {
        var dto = new Models.AI.AITaskDto
        {
            Name = "Task",
            Cron = "0 0 * * *",
            Repository = "https://github.com/x/y",
            NodeFailurePolicyJson = "{\"maxRetries\":5}"
        };

        Assert.Contains("maxRetries", dto.ToCreateTaskDto().NodeFailurePolicyJson!);
    }

    [Fact]
    public void AITaskDto_DefaultValues_AreCorrect()
    {
        var dto = new Models.AI.AITaskDto();

        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal("main", dto.BaseBranch);
        Assert.Equal("new", dto.BranchStrategy);
        Assert.Equal(600, dto.MaxRuntimeSeconds);
        Assert.Equal(50, dto.MaxFileChanges);
        Assert.True(dto.IsEnabled);
        Assert.Equal(1, dto.MaxConcurrentRuns);
    }

    // ---- Fakes ----

    private sealed class FakeWorkspacePreparationService : IWorkspacePreparationService
    {
        public Task<WorkspacePreparationResult> PrepareAsync(ScheduledTask task, Guid executionId, CancellationToken ct = default)
            => Task.FromResult(new WorkspacePreparationResult("/tmp/fake-ws", "chronocode/fake"));
    }
}
