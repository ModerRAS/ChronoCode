using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Unit tests for WorkspacePreparationService and ChatRuntimeService.
/// </summary>
public class ServiceTests
{
    // ---- WorkspacePreparationService ----

    private static ScheduledTask MakeTask(BranchStrategy strategy = BranchStrategy.New) => new()
    {
        Id = Guid.NewGuid(),
        Name = "test",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        BranchStrategy = strategy,
        MaxFileChanges = 50,
        MaxRuntimeSeconds = 600,
        WorkflowDefinitionJson = "{}",
        NodeFailurePolicyJson = "{}",
        CreatedAt = DateTime.UtcNow,
        LastStatus = TaskStatus.Pending
    };

    [Fact]
    public async Task WorkspacePreparation_NewBranch_ClonesAndCreatesBranch()
    {
        var git = new RecordingGitService();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TaskRunner:WorkspaceBasePath"] = "/tmp/workspaces"
            })
            .Build();

        var service = new WorkspacePreparationService(git, config, NullLogger<WorkspacePreparationService>.Instance);
        var task = MakeTask(BranchStrategy.New);

        var result = await service.PrepareAsync(task, Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Contains(task.Id.ToString(), result.WorkspacePath);
        Assert.StartsWith("chronocode/", result.BranchName);
        Assert.Equal(1, git.CloneCalls);
        Assert.Equal(1, git.CreateBranchCalls);
        Assert.Equal(1, git.CheckoutCalls);
        Assert.Equal(task.RepositoryUrl, git.LastCloneUrl);
        Assert.Equal(task.BaseBranch, git.LastBaseBranch);
    }

    [Fact]
    public async Task WorkspacePreparation_ReuseBranch_UsesMainBranchName()
    {
        var git = new RecordingGitService();
        var config = new ConfigurationBuilder().Build();
        var service = new WorkspacePreparationService(git, config, NullLogger<WorkspacePreparationService>.Instance);
        var task = MakeTask(BranchStrategy.Reuse);

        var result = await service.PrepareAsync(task, Guid.NewGuid());

        Assert.Equal("chronocode/main", result.BranchName);
    }

    [Fact]
    public async Task WorkspacePreparation_DefaultBasePath_WhenConfigMissing()
    {
        var git = new RecordingGitService();
        var config = new ConfigurationBuilder().Build();
        var service = new WorkspacePreparationService(git, config, NullLogger<WorkspacePreparationService>.Instance);
        var task = MakeTask();

        var result = await service.PrepareAsync(task, Guid.NewGuid());

        Assert.StartsWith("/workspaces", result.WorkspacePath);
    }

    [Fact]
    public async Task WorkspacePreparation_UniqueWorkspacePerTask()
    {
        var git = new RecordingGitService();
        var config = new ConfigurationBuilder().Build();
        var service = new WorkspacePreparationService(git, config, NullLogger<WorkspacePreparationService>.Instance);

        var task1 = MakeTask();
        var task2 = MakeTask();

        var r1 = await service.PrepareAsync(task1, Guid.NewGuid());
        var r2 = await service.PrepareAsync(task2, Guid.NewGuid());

        // Different tasks get different workspace paths (contains task ID)
        Assert.NotEqual(r1.WorkspacePath, r2.WorkspacePath);
        Assert.Contains(task1.Id.ToString(), r1.WorkspacePath);
        Assert.Contains(task2.Id.ToString(), r2.WorkspacePath);
    }

    // ---- ChatRuntimeService ----

    [Fact]
    public async Task ChatRuntime_SendMessage_ReturnsRuntimeResponse()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("AI says hello");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        var result = await service.SendChatMessageAsync("help me");

        Assert.Equal("AI says hello", result);
        Assert.Equal(1, runtime.EnsureCalls);
        Assert.Equal(1, runtime.SendCalls);
    }

    [Fact]
    public async Task ChatRuntime_SendMessage_IncludesUserMessageInPrompt()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("response");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        await service.SendChatMessageAsync("create a daily build");

        Assert.Contains("create a daily build", runtime.LastPrompt);
    }

    [Fact]
    public async Task ChatRuntime_SendMessage_IncludesSystemPromptInstructions()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("response");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        await service.SendChatMessageAsync("test");

        Assert.Contains("ChronoCode", runtime.LastPrompt!);
        Assert.Contains("create_task", runtime.LastPrompt!);
        Assert.Contains("workflow_definition_json", runtime.LastPrompt!);
    }

    [Fact]
    public async Task ChatRuntime_SendMessage_CleansUpWorkingDirectory()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("ok");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        await service.SendChatMessageAsync("test");

        Assert.False(Directory.Exists(runtime.LastWorkingDirectory));
    }

    [Fact]
    public async Task ChatRuntime_SendMessage_EnsuresRuntimeReady()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("ok");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        await service.SendChatMessageAsync("test");

        Assert.Equal(1, runtime.EnsureReadyCalls);
    }

    [Fact]
    public async Task ChatRuntime_SendMessage_CreatesUniqueExecutionId()
    {
        var runtime = new FakeChatAgentRuntime();
        runtime.EnqueueResponse("ok");
        runtime.EnqueueResponse("ok2");
        var resolver = new FakeChatResolver(runtime);
        var service = new ChatRuntimeService(resolver, NullLogger<ChatRuntimeService>.Instance);

        await service.SendChatMessageAsync("first");
        var exec1 = runtime.LastExecutionId;
        await service.SendChatMessageAsync("second");
        var exec2 = runtime.LastExecutionId;

        Assert.NotEqual(exec1, exec2);
    }

    // ---- Fakes ----

    private sealed class RecordingGitService : IGitService
    {
        public int CloneCalls, CreateBranchCalls, CheckoutCalls;
        public string? LastCloneUrl, LastBaseBranch;

        public Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath)
        { CloneCalls++; LastCloneUrl = repoUrl; return Task.FromResult(workspacePath); }

        public Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch)
        { CreateBranchCalls++; LastBaseBranch = baseBranch; return Task.FromResult(branchName); }

        public Task CheckoutBranchAsync(string repoPath, string branchName)
        { CheckoutCalls++; return Task.CompletedTask; }

        public Task<string> CommitChangesAsync(string repoPath, string message) => throw new NotImplementedException();
        public Task PushChangesAsync(string repoPath, string remoteName = "origin") => throw new NotImplementedException();
        public Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body) => throw new NotImplementedException();
        public Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath) => throw new NotImplementedException();
    }

    private sealed class FakeChatAgentRuntime : IAgentRuntime
    {
        private readonly Queue<string> _responses = new();
        public int EnsureReadyCalls, EnsureCalls, SendCalls;
        public string? LastPrompt;
        public string? LastWorkingDirectory;
        public Guid LastExecutionId;

        public void EnqueueResponse(string resp) => _responses.Enqueue(resp);

        public AgentRuntimeStatus GetStatus() => new("pi", true, null, true, true, true);
        public Task EnsureReadyAsync(CancellationToken ct = default) { EnsureReadyCalls++; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<AgentExecutionSession> EnsureExecutionSessionAsync(Guid executionId, string workingDir, Func<string, Task> onChunk, string? sessionRef = null, CancellationToken ct = default)
        {
            EnsureCalls++;
            LastWorkingDirectory = workingDir;
            LastExecutionId = executionId;
            return Task.FromResult(new AgentExecutionSession("pi", executionId.ToString(), null, workingDir, true));
        }

        public Task<string> SendMessageAsync(Guid executionId, string workingDir, string prompt, AgentMessageMode mode, Func<string, Task> onChunk, CancellationToken ct = default)
        {
            SendCalls++;
            LastPrompt = prompt;
            return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : "default");
        }

        public Task<AgentExecutionSession?> GetExecutionSessionAsync(Guid executionId, CancellationToken ct = default)
            => Task.FromResult<AgentExecutionSession?>(null);

        public Task<AgentExecutionSession> ResumeExecutionSessionAsync(Guid executionId, string workingDir, string sessionRef, Func<string, Task> onChunk, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task StopExecutionAsync(Guid executionId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeChatResolver : IAgentRuntimeResolver
    {
        private readonly FakeChatAgentRuntime _rt;
        public FakeChatResolver(FakeChatAgentRuntime rt) => _rt = rt;
        public IAgentRuntime Get(string? backend) => _rt;
        public AgentRuntimeStatus GetStatus(string? backend) => _rt.GetStatus();
    }
}
