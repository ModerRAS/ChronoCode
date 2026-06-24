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
/// Direct unit tests for AgentNodeExecutor: valid output, schema repair
/// (success + failure), external retry classification, terminal failure,
/// opencode rejection, session resume, and template rendering.
/// </summary>
public class AgentNodeExecutorTests
{
    private static ScheduledTask MakeTask() => new()
    {
        Id = Guid.NewGuid(),
        Name = "test-task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        MaxRuntimeSeconds = 60,
        MaxFileChanges = 50,
        WorkflowDefinitionJson = "{}",
        NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson(),
        RuntimeBackend = WorkflowBackend.Pi,
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

    private static WorkflowNodeExecution MakeNode() => new()
    {
        Id = Guid.NewGuid(),
        ExecutionId = Guid.NewGuid(),
        NodeId = "agent",
        NodeType = "agent",
        ScopeKey = "root",
        Attempt = 0,
        Status = WorkflowNodeStatus.Running,
        StartedAt = DateTime.UtcNow,
        AgentBackend = WorkflowBackend.Pi
    };

    private static AgentWorkflowNode MakeDef(WorkflowDataContract? contract = null) => new()
    {
        NodeId = "agent",
        Name = "Agent",
        Backend = WorkflowBackend.Pi,
        PromptTemplate = "do the task",
        DataContract = contract ?? new WorkflowDataContract(),
        NextNodeId = "end"
    };

    private static WorkflowContext CtxWithWorkspace()
    {
        var ctx = new WorkflowContext();
        ctx.Root["run"] = new JsonObject { ["workspacePath"] = "/tmp/fake-ws" };
        ctx.Root["task"] = new JsonObject { ["name"] = "test-task" };
        return ctx;
    }

    private static (AgentNodeExecutor executor, FakeAgentRuntime runtime, InMemoryExecutionRepository execRepo) Create()
    {
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var runtime = new FakeAgentRuntime();
        var resolver = new FakeResolver(runtime);
        var executor = new AgentNodeExecutor(resolver, execRepo, NullLogger<AgentNodeExecutor>.Instance);
        return (executor, runtime, execRepo);
    }

    private const string ValidEnvelope = """{"status":"completed","passed":true,"summary":"ok","artifacts":[],"data":{}}""";

    [Fact]
    public async Task Execute_ValidResponse_Completes()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend(ValidEnvelope);
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal("end", result.NextNodeId);
        Assert.False(result.Paused);
        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
        Assert.NotNull(node.OutputJson);
        Assert.Null(node.LeaseExpiresAt);
        Assert.Equal(1, runtime.SendCalls);
    }

    [Fact]
    public async Task Execute_RendersPromptTemplate()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend(ValidEnvelope);
        var node = MakeNode();
        var def = MakeDef();
        def.PromptTemplate = "task={{$.task.name}}";
        var ctx = CtxWithWorkspace();

        await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal("task=test-task", runtime.LastPrompt);
    }

    [Fact]
    public async Task Execute_InvalidThenRepair_Succeeds()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // missing status
        runtime.EnqueueSend(ValidEnvelope); // repair succeeds
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal("end", result.NextNodeId);
        Assert.Equal(WorkflowNodeStatus.Completed, node.Status);
        Assert.True(node.SchemaRepairAttempted);
        Assert.Equal(2, runtime.SendCalls);
    }

    [Fact]
    public async Task Execute_InvalidThenRepairFails_TerminallyFails()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // invalid
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // still invalid
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.True(result.Failed);
        Assert.Equal(WorkflowNodeStatus.SchemaValidationFailed, node.Status);
        Assert.Equal(WorkflowFailureReason.SchemaValidationFailed, node.FailureReason);
        Assert.True(node.SchemaRepairAttempted);
    }

    [Fact]
    public async Task Execute_AlreadyRepaired_FailsImmediately()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // invalid
        var node = MakeNode();
        node.SchemaRepairAttempted = true; // repair already used
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.True(result.Failed);
        Assert.Equal(WorkflowNodeStatus.SchemaValidationFailed, node.Status);
        Assert.Equal(1, runtime.SendCalls); // no repair attempt
    }

    [Fact]
    public async Task Execute_RetryableException_PausesForRetry()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueThrow(new InvalidOperationException("llm api down"));
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.True(result.Paused);
        Assert.False(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Retrying, node.Status);
        Assert.Equal(1, node.RetryCount);
        Assert.NotNull(node.NextRetryAt);
    }

    [Fact]
    public async Task Execute_RetryableException_AtMaxAttempts_TerminallyFails()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueThrow(new InvalidOperationException("llm api down"));
        var node = MakeNode();
        node.Attempt = 2; // default MaxAttempts=3, so 2+1=3 is not < 3
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.True(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Failed, node.Status);
        Assert.Null(node.NextRetryAt);
    }

    [Fact]
    public async Task Execute_NonRetryableException_TerminallyFails()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueThrow(new ArgumentException("not retryable"));
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.True(result.Failed);
        Assert.Equal(WorkflowNodeStatus.Failed, node.Status);
        Assert.Null(node.NextRetryAt);
    }

    [Fact]
    public async Task Execute_OpencodeBackend_Throws()
    {
        var (executor, _, _) = Create();
        var node = MakeNode();
        var def = MakeDef();
        def.Backend = WorkflowBackend.Opencode;
        var ctx = CtxWithWorkspace();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default));
    }

    [Fact]
    public async Task Execute_MissingWorkspace_Throws()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend(ValidEnvelope);
        var node = MakeNode();
        var def = MakeDef();
        var ctx = new WorkflowContext(); // no workspace

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default));
    }

    [Fact]
    public async Task Execute_WithExistingSessionRef_ResumesSession()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend(ValidEnvelope);
        var node = MakeNode();
        node.AgentSessionFile = "existing-session-file";
        node.AgentSessionId = "existing-session-id";
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal(1, runtime.ResumeCalls);
        Assert.Equal(0, runtime.EnsureCalls);
        Assert.Equal("existing-session-file", runtime.LastResumeSessionRef);
    }

    [Fact]
    public async Task Execute_WithoutSessionRef_CreatesNewSession()
    {
        var (executor, runtime, _) = Create();
        runtime.EnqueueSend(ValidEnvelope);
        var node = MakeNode();
        var def = MakeDef();
        var ctx = CtxWithWorkspace();

        await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal(1, runtime.EnsureCalls);
        Assert.Equal(0, runtime.ResumeCalls);
    }

    [Fact]
    public async Task Execute_DataContractValidation_FailsOnMissingRequiredField()
    {
        var (executor, runtime, _) = Create();
        // Valid envelope but missing required data field
        runtime.EnqueueSend("""{"status":"completed","summary":"ok","artifacts":[],"data":{}}""");
        // Repair: include the required field
        runtime.EnqueueSend("""{"status":"completed","passed":true,"summary":"ok","artifacts":[],"data":{"result":"done"}}""");
        var node = MakeNode();
        var def = MakeDef(new WorkflowDataContract
        {
            Fields = [new WorkflowDataFieldContract { Name = "result", Type = WorkflowDataType.String, Required = true }]
        });
        var ctx = CtxWithWorkspace();

        var result = await executor.ExecuteAsync(node, def, ctx, MakeRun(), MakeTask(), default);

        Assert.Equal("end", result.NextNodeId);
        Assert.True(node.SchemaRepairAttempted);
    }

    // ---- Fakes ----

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        private readonly Queue<Func<Task<string>>> _sendScript = new();
        public int EnsureCalls, ResumeCalls, SendCalls;
        public string? LastPrompt;
        public string? LastResumeSessionRef;

        public void EnqueueSend(string resp) => _sendScript.Enqueue(() => Task.FromResult(resp));
        public void EnqueueThrow(Exception ex) => _sendScript.Enqueue(() => Task.FromException<string>(ex));

        public AgentRuntimeStatus GetStatus() => new("pi", true, null, true, true, true);
        public Task EnsureReadyAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<AgentExecutionSession> EnsureExecutionSessionAsync(Guid executionId, string workingDir, Func<string, Task> onChunk, string? sessionRef = null, CancellationToken ct = default)
        {
            EnsureCalls++;
            return Task.FromResult(new AgentExecutionSession("pi", executionId.ToString(), "pi-session-file", workingDir, true));
        }

        public Task<AgentExecutionSession> ResumeExecutionSessionAsync(Guid executionId, string workingDir, string sessionRef, Func<string, Task> onChunk, CancellationToken ct = default)
        {
            ResumeCalls++;
            LastResumeSessionRef = sessionRef;
            return Task.FromResult(new AgentExecutionSession("pi", executionId.ToString(), "pi-session-file", workingDir, true));
        }

        public Task<string> SendMessageAsync(Guid executionId, string workingDir, string prompt, AgentMessageMode mode, Func<string, Task> onChunk, CancellationToken ct = default)
        {
            SendCalls++;
            LastPrompt = prompt;
            return _sendScript.Count > 0 ? _sendScript.Dequeue()() : Task.FromResult(ValidEnvelope);
        }

        public Task<AgentExecutionSession?> GetExecutionSessionAsync(Guid executionId, CancellationToken ct = default)
            => Task.FromResult<AgentExecutionSession?>(null);

        public Task StopExecutionAsync(Guid executionId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeResolver : IAgentRuntimeResolver
    {
        private readonly FakeAgentRuntime _rt;
        public FakeResolver(FakeAgentRuntime rt) => _rt = rt;
        public IAgentRuntime Get(string? backend) => _rt;
        public AgentRuntimeStatus GetStatus(string? backend) => _rt.GetStatus();
    }
}
