using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using ChronoCode.Services.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Engine-level integration tests for WorkflowRunService: schema repair, external retry
/// with session resume, stuck-lease recovery, condition/for_each/while/parallel control
/// flow, and approval-gate pause/resume. Uses in-memory repos + fake runtimes.
/// </summary>
public class WorkflowRunServiceTests
{
    private static (WorkflowRunService svc, InMemoryExecutionRepository execRepo, InMemoryTaskRepository taskRepo, FakeAgentRuntime runtime) CreateService()
    {
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var runtime = new FakeAgentRuntime();
        var resolver = new FakeAgentRuntimeResolver(runtime);
        var svc = new WorkflowRunService(
            execRepo,
            taskRepo,
            new FakeWorkspacePreparationService(),
            new FakeGitService(),
            resolver,
            NullLogger<WorkflowRunService>.Instance,
            NullLoggerFactory.Instance);
        return (svc, execRepo, taskRepo, runtime);
    }

    private static async Task<ScheduledTask> CreateTaskAsync(InMemoryTaskRepository taskRepo, string workflowJson, string? inputsJson = null)
    {
        var dto = new CreateTaskDto
        {
            Name = "test-" + Guid.NewGuid().ToString("N")[..8],
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            BranchStrategy = BranchStrategy.New,
            MaxRuntimeSeconds = 60,
            MaxFileChanges = 50,
            IsEnabled = true,
            WorkflowDefinitionJson = workflowJson,
            DefaultInputsJson = inputsJson,
            RuntimeBackend = WorkflowBackend.Pi,
            MaxConcurrentRuns = 1,
            NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson()
        };
        return await taskRepo.CreateAsync(dto);
    }

    private static string WorkflowJson(List<WorkflowNode> nodes, string startNodeId) =>
        WorkflowDefinitionSerializer.Serialize(new WorkflowDefinition { Version = 1, StartNodeId = startNodeId, Nodes = nodes });

    private static string AgentWorkflowJson(WorkflowDataContract? contract = null) => WorkflowJson(
    [
        new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
        new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "agent" },
        new AgentWorkflowNode
        {
            NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi,
            PromptTemplate = "do the task", DataContract = contract ?? new WorkflowDataContract(),
            NextNodeId = "end"
        },
        new EndWorkflowNode { NodeId = "end", Name = "End" }
    ], "start");

    private const string ValidEnvelope = """{"status":"completed","passed":true,"summary":"ok","artifacts":[],"data":{}}""";

    [Fact]
    public async Task SchemaRepair_BadThenGood_Succeeds()
    {
        // Plan: "第一次返回缺少 status 的 JSON，第二次返回合法 JSON"
        var (svc, execRepo, taskRepo, runtime) = CreateService();
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // invalid: missing status
        runtime.EnqueueSend("""{"status":"completed","passed":true,"summary":"ok","artifacts":[],"data":{"summary":"repaired"}}""");

        var contract = new WorkflowDataContract
        {
            Fields = [new WorkflowDataFieldContract { Name = "summary", Type = WorkflowDataType.String, Required = true }]
        };
        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson(contract));
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var agentNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agent");
        Assert.Equal(WorkflowNodeStatus.Completed, agentNode.Status);
        Assert.True(agentNode.SchemaRepairAttempted);
        Assert.Contains("repaired", agentNode.OutputJson);
    }

    [Fact]
    public async Task SchemaRepair_BadThenBad_Fails()
    {
        var (svc, execRepo, taskRepo, runtime) = CreateService();
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // invalid: missing status
        runtime.EnqueueSend("""{"summary":"ok","artifacts":[]}"""); // still invalid

        var contract = new WorkflowDataContract
        {
            Fields = [new WorkflowDataFieldContract { Name = "summary", Type = WorkflowDataType.String, Required = true }]
        };
        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson(contract));
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Failed, exec.Status);
        var agentNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agent");
        Assert.Equal(WorkflowNodeStatus.SchemaValidationFailed, agentNode.Status);
        Assert.Equal(WorkflowFailureReason.SchemaValidationFailed, agentNode.FailureReason);

        // Plan: "后续节点不执行" — the end node must NOT have a node-execution record.
        var allNodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.DoesNotContain(allNodes, n => n.NodeId == "end");
    }

    [Fact]
    public async Task ExternalRetry_ThenResume_Succeeds()
    {
        var (svc, execRepo, taskRepo, runtime) = CreateService();
        runtime.EnqueueThrow(new InvalidOperationException("llm api down"));
        runtime.EnqueueSend(ValidEnvelope);

        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson());
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        // First attempt threw a retryable error -> node is retrying, run paused (still Running).
        Assert.Equal(Models.TaskStatus.Running, exec.Status);
        var agentNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agent");
        Assert.Equal(WorkflowNodeStatus.Retrying, agentNode.Status);
        Assert.False(string.IsNullOrWhiteSpace(agentNode.AgentSessionFile));
        Assert.Equal(1, runtime.EnsureCalls);
        Assert.Equal(0, runtime.ResumeCalls);

        // Force the retry due into the past and re-drive the run.
        agentNode.NextRetryAt = DateTime.UtcNow.AddSeconds(-1);
        await execRepo.UpdateNodeExecutionAsync(agentNode);
        await svc.ContinueRunAsync(exec.Id);

        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Completed, exec2!.Status);
        Assert.Equal(1, runtime.ResumeCalls);
        // Plan: "sessionRef 取自持久化的 AgentSessionFile ?? AgentSessionId"
        Assert.Equal(agentNode.AgentSessionFile, runtime.LastResumeSessionRef);
        var agentNode2 = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agent");
        Assert.Equal(WorkflowNodeStatus.Completed, agentNode2.Status);
    }

    [Fact]
    public async Task StuckLease_AgentNode_BelowMaxAttempts_Retries()
    {
        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson());
        var exec = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = task.WorkflowDefinitionJson
        };
        await execRepo.CreateAsync(exec);
        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = exec.Id,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.Running,
            StartedAt = DateTime.UtcNow,
            LeaseExpiresAt = DateTime.UtcNow.AddSeconds(-10),
            AgentBackend = WorkflowBackend.Pi
        };
        await execRepo.CreateNodeExecutionAsync(node);

        await svc.RecoverStuckNodesAsync();

        var recovered = await execRepo.GetNodeExecutionAsync(node.Id);
        Assert.Equal(WorkflowNodeStatus.Retrying, recovered!.Status);
        Assert.NotNull(recovered.NextRetryAt);
        Assert.Equal(1, recovered.RetryCount);
    }

    [Fact]
    public async Task StuckLease_AgentNode_AtMaxAttempts_Fails()
    {
        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson());
        var exec = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = task.WorkflowDefinitionJson
        };
        await execRepo.CreateAsync(exec);
        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = exec.Id,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 2, // default policy MaxAttempts=3 -> 2+1=3 not < 3 -> terminal failure
            Status = WorkflowNodeStatus.Running,
            StartedAt = DateTime.UtcNow,
            LeaseExpiresAt = DateTime.UtcNow.AddSeconds(-10),
            AgentBackend = WorkflowBackend.Pi
        };
        await execRepo.CreateNodeExecutionAsync(node);

        await svc.RecoverStuckNodesAsync();

        var recovered = await execRepo.GetNodeExecutionAsync(node.Id);
        Assert.Equal(WorkflowNodeStatus.Failed, recovered!.Status);
        Assert.Equal(WorkflowFailureReason.Timeout, recovered.FailureReason);
        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Failed, exec2!.Status);
    }

    [Fact]
    public async Task Condition_TrueBranch_RunsTrueNodeOnly()
    {
        // Plan: "condition 节点根据 $.nodes.review.output.passed=true 走 true 分支"
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "review" },
            new AgentWorkflowNode { NodeId = "review", Name = "Review", Backend = WorkflowBackend.Pi, PromptTemplate = "review", DataContract = new(), NextNodeId = "cond" },
            new ConditionWorkflowNode
            {
                NodeId = "cond", Name = "Cond",
                Predicate = new ComparisonWorkflowPredicate { Path = "$.nodes.review.output.passed", Operator = WorkflowComparisonOperator.Truthy },
                TrueNodeId = "agentT", FalseNodeId = "agentF"
            },
            new AgentWorkflowNode { NodeId = "agentT", Name = "T", Backend = WorkflowBackend.Pi, PromptTemplate = "t", DataContract = new(), NextNodeId = "end" },
            new AgentWorkflowNode { NodeId = "agentF", Name = "F", Backend = WorkflowBackend.Pi, PromptTemplate = "f", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, runtime) = CreateService();
        // Review agent returns passed=true
        runtime.EnqueueSend("""{"status":"completed","passed":true,"summary":"review passed","artifacts":[],"data":{}}""");
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var nodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.Contains(nodes, n => n.NodeId == "agentT");
        Assert.DoesNotContain(nodes, n => n.NodeId == "agentF");
    }

    [Fact]
    public async Task Condition_FalseBranch_RunsFalseNodeOnly()
    {
        // Plan: "改成 false 走 false 分支"
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "review" },
            new AgentWorkflowNode { NodeId = "review", Name = "Review", Backend = WorkflowBackend.Pi, PromptTemplate = "review", DataContract = new(), NextNodeId = "cond" },
            new ConditionWorkflowNode
            {
                NodeId = "cond", Name = "Cond",
                Predicate = new ComparisonWorkflowPredicate { Path = "$.nodes.review.output.passed", Operator = WorkflowComparisonOperator.Truthy },
                TrueNodeId = "agentT", FalseNodeId = "agentF"
            },
            new AgentWorkflowNode { NodeId = "agentT", Name = "T", Backend = WorkflowBackend.Pi, PromptTemplate = "t", DataContract = new(), NextNodeId = "end" },
            new AgentWorkflowNode { NodeId = "agentF", Name = "F", Backend = WorkflowBackend.Pi, PromptTemplate = "f", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, runtime) = CreateService();
        // Review agent returns passed=false
        runtime.EnqueueSend("""{"status":"completed","passed":false,"summary":"review failed","artifacts":[],"data":{}}""");
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var nodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.Contains(nodes, n => n.NodeId == "agentF");
        Assert.DoesNotContain(nodes, n => n.NodeId == "agentT");
    }

    [Fact]
    public async Task ForEach_ThreeItems_RunsBodyThrice()
    {
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "fe" },
            new ForEachWorkflowNode
            {
                NodeId = "fe", Name = "FE", CollectionPath = "$.inputs.items", ItemVariable = "item",
                BodyStartNodeId = "agent", NextNodeId = "end", MaxIterations = 10
            },
            new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "do {{$.item}}", DataContract = new(), NextNodeId = "fe" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, wf, """{"items":["a","b","c"]}""");
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var agentNodes = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Where(n => n.NodeId == "agent").ToList();
        Assert.Equal(3, agentNodes.Count);
        Assert.Equal(3, agentNodes.Select(n => n.ScopeKey).Distinct().Count());
    }

    [Fact]
    public async Task ForEach_MaxIterationsCaps_RunsOnlyMaxIterations()
    {
        // Plan: for_each has maxIterations; when collection exceeds it, only maxIterations bodies run.
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "fe" },
            new ForEachWorkflowNode
            {
                NodeId = "fe", Name = "FE", CollectionPath = "$.inputs.items", ItemVariable = "item",
                BodyStartNodeId = "agent", NextNodeId = "end", MaxIterations = 2
            },
            new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "do {{$.item}}", DataContract = new(), NextNodeId = "fe" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        // 5 items but maxIterations=2 — only 2 iterations should run, then continue to end.
        var task = await CreateTaskAsync(taskRepo, wf, """{"items":["a","b","c","d","e"]}""");
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var agentNodes = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Where(n => n.NodeId == "agent").ToList();
        Assert.Equal(2, agentNodes.Count);
    }

    [Fact]
    public async Task Parallel_AllCompleted_CompletesEvenWithFailedPassed()
    {
        // Plan: parallel joinMode=all_completed allows continuation regardless of branch passed values.
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "par" },
            new ParallelWorkflowNode
            {
                NodeId = "par", Name = "Par",
                BranchStartNodeIds = ["agentB1", "agentB2"],
                JoinMode = WorkflowParallelJoinMode.AllCompleted,
                NextNodeId = "end"
            },
            new AgentWorkflowNode { NodeId = "agentB1", Name = "B1", Backend = WorkflowBackend.Pi, PromptTemplate = "b1", DataContract = new(), NextNodeId = "end" },
            new AgentWorkflowNode { NodeId = "agentB2", Name = "B2", Backend = WorkflowBackend.Pi, PromptTemplate = "b2", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, runtime) = CreateService();
        // Branch B1 returns passed=false; with all_completed the run should still complete.
        runtime.EnqueueSend("""{"status":"completed","passed":false,"summary":"branch b1 failed check","artifacts":[],"data":{}}""");
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var nodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.Contains(nodes, n => n.NodeId == "agentB1");
        Assert.Contains(nodes, n => n.NodeId == "agentB2");
    }

    [Fact]
    public async Task While_AlwaysTrue_MaxIterations2_FailsAfterTwoBodies()
    {
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "wh" },
            new WhileWorkflowNode
            {
                NodeId = "wh", Name = "WH",
                Predicate = new ConstantWorkflowPredicate { Value = true },
                BodyStartNodeId = "agent", NextNodeId = "end", MaxIterations = 2
            },
            new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "loop", DataContract = new(), NextNodeId = "wh" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Failed, exec.Status);
        var agentNodes = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Where(n => n.NodeId == "agent").ToList();
        Assert.Equal(2, agentNodes.Count);
    }

    [Fact]
    public async Task Parallel_AllSucceeded_RunsAllBranchesAndCompletes()
    {
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "par" },
            new ParallelWorkflowNode
            {
                NodeId = "par", Name = "Par",
                BranchStartNodeIds = ["agentB1", "agentB2"],
                JoinMode = WorkflowParallelJoinMode.AllSucceeded,
                NextNodeId = "end"
            },
            new AgentWorkflowNode { NodeId = "agentB1", Name = "B1", Backend = WorkflowBackend.Pi, PromptTemplate = "b1", DataContract = new(), NextNodeId = "end" },
            new AgentWorkflowNode { NodeId = "agentB2", Name = "B2", Backend = WorkflowBackend.Pi, PromptTemplate = "b2", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        Assert.Equal(Models.TaskStatus.Completed, exec.Status);
        var nodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.Contains(nodes, n => n.NodeId == "agentB1");
        Assert.Contains(nodes, n => n.NodeId == "agentB2");
    }

    [Fact]
    public async Task ApprovalGate_PausesThenApprove_Completes()
    {
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "gate" },
            new ApprovalGateWorkflowNode { NodeId = "gate", Name = "Gate", Message = "approve me", NextNodeId = "agent" },
            new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "go", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        // Paused at the gate.
        Assert.Equal(Models.TaskStatus.Running, exec.Status);
        var gateNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "gate");
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, gateNode.Status);

        await svc.ApproveNodeAsync(exec.Id, gateNode.Id, true, null);

        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Completed, exec2!.Status);
        var agentNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agent");
        Assert.Equal(WorkflowNodeStatus.Completed, agentNode.Status);
    }

    [Fact]
    public async Task ApprovalGate_Reject_PropagatesTaskLastStatusAndError()
    {
        // Oracle gap: ApproveNodeAsync(approved:false) called FailRunAsync with null
        // task, so task.LastStatus / LastError were never updated on rejection.
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "gate" },
            new ApprovalGateWorkflowNode { NodeId = "gate", Name = "Gate", Message = "approve me", NextNodeId = "agent" },
            new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "go", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        // Paused at the gate.
        var gateNode = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "gate");
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, gateNode.Status);

        await svc.ApproveNodeAsync(exec.Id, gateNode.Id, false, "rejected by reviewer");

        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Failed, exec2!.Status);

        // The bug: task.LastStatus/LastError stayed Pending/null because FailRunAsync
        // was called with null task. After the fix they must reflect the failure.
        var taskAfter = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(taskAfter);
        Assert.Equal(Models.TaskStatus.Failed, taskAfter!.LastStatus);
        Assert.NotNull(taskAfter.LastError);
        Assert.Contains("Approval rejected", taskAfter.LastError);
    }

    [Fact]
    public async Task StuckLease_AtMaxAttempts_Fails_PropagatesTaskLastStatusAndError()
    {
        // Oracle gap: RecoverStuckNodesAsync terminal-fail path called FailRunAsync
        // with null task, so task.LastStatus / LastError were never updated on lease
        // expiry. Mirrors StuckLease_AgentNode_AtMaxAttempts_Fails but additionally
        // asserts the task-level state propagated.
        var (svc, execRepo, taskRepo, _) = CreateService();
        var task = await CreateTaskAsync(taskRepo, AgentWorkflowJson());
        var exec = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = task.WorkflowDefinitionJson
        };
        await execRepo.CreateAsync(exec);
        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = exec.Id,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 2, // default policy MaxAttempts=3 -> terminal failure
            Status = WorkflowNodeStatus.Running,
            StartedAt = DateTime.UtcNow,
            LeaseExpiresAt = DateTime.UtcNow.AddSeconds(-10),
            AgentBackend = WorkflowBackend.Pi
        };
        await execRepo.CreateNodeExecutionAsync(node);

        await svc.RecoverStuckNodesAsync();

        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Failed, exec2!.Status);

        var taskAfter = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(taskAfter);
        Assert.Equal(Models.TaskStatus.Failed, taskAfter!.LastStatus);
        Assert.NotNull(taskAfter.LastError);
        Assert.Contains("lease expired", taskAfter.LastError);
    }

    [Fact]
    public async Task Combined_Parallel_Condition_Approval_Smoke()
    {
        // Mirrors the plan's manual smoke: two parallel branches; branch A returns
        // passed=false -> condition routes to the false branch (approval gate) ->
        // approve -> agent -> end.
        var wf = WorkflowJson(
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
            new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "par" },
            new ParallelWorkflowNode
            {
                NodeId = "par", Name = "Par",
                BranchStartNodeIds = ["agentA", "agentB"],
                JoinMode = WorkflowParallelJoinMode.AllSucceeded,
                NextNodeId = "cond"
            },
            new AgentWorkflowNode { NodeId = "agentA", Name = "A", Backend = WorkflowBackend.Pi, PromptTemplate = "a", DataContract = new(), NextNodeId = "cond" },
            new AgentWorkflowNode { NodeId = "agentB", Name = "B", Backend = WorkflowBackend.Pi, PromptTemplate = "b", DataContract = new(), NextNodeId = "cond" },
            new ConditionWorkflowNode
            {
                NodeId = "cond", Name = "Cond",
                Predicate = new ComparisonWorkflowPredicate { Path = "$.nodes.agentA.output.passed", Operator = WorkflowComparisonOperator.Truthy },
                TrueNodeId = "end", FalseNodeId = "gate"
            },
            new ApprovalGateWorkflowNode { NodeId = "gate", Name = "Gate", Message = "approve", NextNodeId = "agentC" },
            new AgentWorkflowNode { NodeId = "agentC", Name = "C", Backend = WorkflowBackend.Pi, PromptTemplate = "c", DataContract = new(), NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ], "start");

        var (svc, execRepo, taskRepo, runtime) = CreateService();
        // Branch A fails the check (passed=false); branch B succeeds (default valid envelope).
        runtime.EnqueueSend("""{"status":"completed","passed":false,"summary":"branch a failed","artifacts":[],"data":{}}""");
        var task = await CreateTaskAsync(taskRepo, wf);
        var exec = await svc.StartRunAsync(task, WorkflowTriggerSource.Manual);

        // Both parallel branches ran; condition routed to the false branch (gate), run paused.
        Assert.Equal(Models.TaskStatus.Running, exec.Status);
        var nodes = await execRepo.GetNodeExecutionsAsync(exec.Id);
        Assert.Contains(nodes, n => n.NodeId == "agentA");
        Assert.Contains(nodes, n => n.NodeId == "agentB");
        var gateNode = nodes.Single(n => n.NodeId == "gate");
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, gateNode.Status);

        await svc.ApproveNodeAsync(exec.Id, gateNode.Id, true, null);

        var exec2 = await execRepo.GetByIdAsync(exec.Id);
        Assert.Equal(Models.TaskStatus.Completed, exec2!.Status);
        var agentC = (await execRepo.GetNodeExecutionsAsync(exec.Id)).Single(n => n.NodeId == "agentC");
        Assert.Equal(WorkflowNodeStatus.Completed, agentC.Status);
    }

    private sealed class FakeAgentRuntime : IAgentRuntime
    {
        private readonly Queue<Func<Task<string>>> _sendScript = new();

        public int EnsureCalls;
        public int ResumeCalls;
        public int SendCalls;
        public string? LastResumeSessionRef;

        public void EnqueueSend(string response) => _sendScript.Enqueue(() => Task.FromResult(response));
        public void EnqueueThrow(Exception ex) => _sendScript.Enqueue(() => Task.FromException<string>(ex));

        public AgentRuntimeStatus GetStatus() => new("pi", true, "http://fake", true, true, true);
        public Task EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<AgentExecutionSession> EnsureExecutionSessionAsync(Guid executionId, string workingDirectory, Func<string, Task> onChunk, string? sessionRef = null, CancellationToken cancellationToken = default)
        {
            EnsureCalls++;
            return Task.FromResult(new AgentExecutionSession("pi", executionId.ToString(), "pi-session-file", workingDirectory, true));
        }

        public Task<AgentExecutionSession> ResumeExecutionSessionAsync(Guid executionId, string workingDirectory, string sessionRef, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
        {
            ResumeCalls++;
            LastResumeSessionRef = sessionRef;
            return Task.FromResult(new AgentExecutionSession("pi", executionId.ToString(), "pi-session-file", workingDirectory, true));
        }

        public Task<string> SendMessageAsync(Guid executionId, string workingDirectory, string prompt, AgentMessageMode mode, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            return _sendScript.Count > 0 ? _sendScript.Dequeue()() : Task.FromResult(ValidEnvelope);
        }

        public Task<AgentExecutionSession?> GetExecutionSessionAsync(Guid executionId, CancellationToken cancellationToken = default)
            => Task.FromResult<AgentExecutionSession?>(new AgentExecutionSession("pi", executionId.ToString(), "pi-session-file", "/tmp/fake", true));

        public Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeAgentRuntimeResolver : IAgentRuntimeResolver
    {
        private readonly FakeAgentRuntime _runtime;
        public FakeAgentRuntimeResolver(FakeAgentRuntime runtime) => _runtime = runtime;
        public IAgentRuntime Get(string? backend) => _runtime;
        public AgentRuntimeStatus GetStatus(string? backend) => _runtime.GetStatus();
    }

    private sealed class FakeWorkspacePreparationService : IWorkspacePreparationService
    {
        public Task<WorkspacePreparationResult> PrepareAsync(ScheduledTask task, Guid executionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspacePreparationResult("/tmp/fake-workspace", "chronocode/fake"));
    }

    private sealed class FakeGitService : IGitService
    {
        public Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath) => throw new NotImplementedException();
        public Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch) => throw new NotImplementedException();
        public Task CheckoutBranchAsync(string repoPath, string branchName) => throw new NotImplementedException();
        public Task<string> CommitChangesAsync(string repoPath, string message) => throw new NotImplementedException();
        public Task PushChangesAsync(string repoPath, string remoteName = "origin") => throw new NotImplementedException();
        public Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body) => throw new NotImplementedException();
        public Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath) => throw new NotImplementedException();
    }
}
