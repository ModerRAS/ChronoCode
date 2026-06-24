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
/// Direct unit tests for CommitChangesNodeExecutor, CreatePullRequestNodeExecutor,
/// and ApprovalGateNodeExecutor. Verifies template rendering, max-file-changes
/// enforcement, and output shape.
/// </summary>
public class NodeExecutorTests
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

    private static WorkflowNodeExecution MakeNode() => new()
    {
        Id = Guid.NewGuid(),
        ExecutionId = Guid.NewGuid(),
        NodeId = "commit",
        NodeType = "commit_changes",
        ScopeKey = "root",
        Attempt = 0,
        Status = WorkflowNodeStatus.Running,
        StartedAt = DateTime.UtcNow,
        AgentBackend = WorkflowBackend.Pi
    };

    private static List<GitFileStatus> Files(params string[] paths) =>
        paths.Select(p => new GitFileStatus { Path = p, Status = "M" }).ToList();

    [Fact]
    public async Task CommitChanges_CommitsAndPushes_WhenFilesChanged()
    {
        var git = new FakeGitService { ChangedFiles = Files("src/file1.ts", "src/file2.ts") };
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var executor = new CommitChangesNodeExecutor(git, execRepo);
        var node = MakeNode();
        var def = new CommitChangesWorkflowNode { NodeId = "commit", Name = "Commit", CommitMessageTemplate = "AI: {{$.task.name}}", NextNodeId = "end" };
        var ctx = CtxWithWorkspace();
        var task = MakeTask();
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, default);

        Assert.Equal("end", result.NextNodeId);
        Assert.False(result.Paused);
        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
        Assert.Equal("mock-commit-sha", run.CommitSha);
        Assert.Equal(2, run.FilesChanged);
        Assert.True(git.CommitCalled);
        Assert.True(git.PushCalled);
        Assert.Equal("AI: test-task", git.LastCommitMessage);
    }

    [Fact]
    public async Task CommitChanges_Fails_WhenTooManyFiles()
    {
        var git = new FakeGitService { ChangedFiles = Files("f0", "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9") };
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var executor = new CommitChangesNodeExecutor(git, execRepo);
        var node = MakeNode();
        var def = new CommitChangesWorkflowNode { NodeId = "commit", Name = "Commit", CommitMessageTemplate = "msg", NextNodeId = "end" };
        var ctx = CtxWithWorkspace();
        var task = MakeTask(maxFileChanges: 5);
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, default);

        Assert.True(result.Failed);
        Assert.Null(result.NextNodeId);
        Assert.Equal(WorkflowNodeStatus.Failed, node.Status);
        Assert.Contains("too_many_files_changed", node.FailureReason);
        Assert.False(git.CommitCalled);
    }

    [Fact]
    public async Task CreatePullRequest_CreatesPR_WithRenderedTemplates()
    {
        var git = new FakeGitService();
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var executor = new CreatePullRequestNodeExecutor(git, execRepo);
        var node = MakeNode();
        node.NodeId = "pr";
        node.NodeType = "create_pull_request";
        var def = new CreatePullRequestWorkflowNode
        {
            NodeId = "pr", Name = "PR",
            TitleTemplate = "PR: {{$.task.name}}",
            BodyTemplate = "Commit: {{$.run.commitSha}}",
            NextNodeId = "end"
        };
        var ctx = CtxWithWorkspace();
        ctx.Run["commitSha"] = "abc123";
        var task = MakeTask();
        var run = MakeRun();
        run.BranchName = "chronocode/test";

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, default);

        Assert.Equal("end", result.NextNodeId);
        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
        Assert.Equal("https://github.com/mock/pr/1", run.PrUrl);
        Assert.Equal("PR: test-task", git.LastPrTitle);
        Assert.Equal("Commit: abc123", git.LastPrBody);
    }

    [Fact]
    public async Task ApprovalGate_PausesWithWaitingApproval()
    {
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var executor = new ApprovalGateNodeExecutor(execRepo);
        var node = MakeNode();
        node.NodeId = "gate";
        node.NodeType = "approval_gate";
        var def = new ApprovalGateWorkflowNode { NodeId = "gate", Name = "Gate", Message = "Please approve", NextNodeId = "end" };
        var ctx = new WorkflowContext();
        var task = MakeTask();
        var run = MakeRun();

        var result = await executor.ExecuteAsync(node, def, ctx, run, task, default);

        Assert.True(result.Paused);
        Assert.False(result.Failed);
        Assert.Null(result.NextNodeId);
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, node.Status);
        Assert.Null(node.LeaseExpiresAt);
    }

    private sealed class FakeGitService : IGitService
    {
        public List<GitFileStatus> ChangedFiles { get; set; } = new();
        public bool CommitCalled { get; private set; }
        public bool PushCalled { get; private set; }
        public string LastCommitMessage { get; private set; } = "";
        public string LastPrTitle { get; private set; } = "";
        public string LastPrBody { get; private set; } = "";

        public Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath) => throw new NotImplementedException();
        public Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch) => throw new NotImplementedException();
        public Task CheckoutBranchAsync(string repoPath, string branchName) => throw new NotImplementedException();

        public Task<string> CommitChangesAsync(string repoPath, string message)
        {
            CommitCalled = true;
            LastCommitMessage = message;
            return Task.FromResult("mock-commit-sha");
        }

        public Task PushChangesAsync(string repoPath, string remoteName = "origin")
        {
            PushCalled = true;
            return Task.CompletedTask;
        }

        public Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
        {
            LastPrTitle = title;
            LastPrBody = body;
            return Task.FromResult("https://github.com/mock/pr/1");
        }

        public Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath)
            => Task.FromResult(ChangedFiles);
    }
}
