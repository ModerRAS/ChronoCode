using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>
/// Workflow run lifecycle: create run + frozen snapshot, dispatch node executors,
/// persist run/node state, resume interrupted runs, recover stuck nodes.
/// </summary>
public sealed class WorkflowRunService : IWorkflowRunService
{
    private const int MaxNodeVisits = 10000;

    private readonly IExecutionRepository _execRepo;
    private readonly ITaskRepository _taskRepo;
    private readonly WorkflowNodeExecutorDispatcher _dispatcher;
    private readonly IAgentRuntimeResolver _resolver;
    private readonly ILogger<WorkflowRunService> _logger;

    public WorkflowRunService(
        IExecutionRepository execRepo,
        ITaskRepository taskRepo,
        IWorkspacePreparationService preparation,
        IGitService git,
        IAgentRuntimeResolver resolver,
        ILogger<WorkflowRunService> logger,
        ILoggerFactory loggerFactory)
    {
        _execRepo = execRepo;
        _taskRepo = taskRepo;
        _resolver = resolver;
        _logger = logger;

        var executors = new INodeExecutor[]
        {
            new PrepareWorkspaceNodeExecutor(preparation),
            new AgentNodeExecutor(resolver, execRepo, loggerFactory.CreateLogger<AgentNodeExecutor>()),
            new ApprovalGateNodeExecutor(execRepo),
            new CommitChangesNodeExecutor(git, execRepo),
            new CreatePullRequestNodeExecutor(git, execRepo)
        };
        _dispatcher = new WorkflowNodeExecutorDispatcher(executors, loggerFactory.CreateLogger<WorkflowNodeExecutorDispatcher>());
    }

    public async Task<TaskExecution> StartRunAsync(ScheduledTask task, string triggerSource, CancellationToken cancellationToken = default)
    {
        var snapshot = WorkflowDefinitionSerializer.Deserialize(task.WorkflowDefinitionJson)
            ?? throw new InvalidOperationException("Task has no valid workflow definition.");

        var execution = new TaskExecution
        {
            TaskId = task.Id,
            Status = Models.TaskStatus.Running,
            WorkflowVersion = task.WorkflowVersion,
            WorkflowSnapshotJson = task.WorkflowDefinitionJson,
            CurrentNodeId = snapshot.StartNodeId,
            TriggerSource = triggerSource,
            StartedAt = DateTime.UtcNow
        };

        var ctx = new WorkflowContext();
        ctx.InitFrom(task, execution, task.DefaultInputsJson);
        execution.WorkflowStateJson = ctx.Serialize();

        await _execRepo.CreateAsync(execution);
        await _execRepo.AddLogAsync(execution.Id, "Info", $"Run started ({triggerSource})", $"workflowVersion={task.WorkflowVersion}; startNode={snapshot.StartNodeId}");

        await ContinueRunAsync(execution.Id, cancellationToken);
        return (await _execRepo.GetByIdAsync(execution.Id))!;
    }

    public async Task ContinueRunAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var execution = await _execRepo.GetByIdAsync(executionId);
        if (execution == null || execution.Status != Models.TaskStatus.Running)
        {
            return;
        }

        var task = await _taskRepo.GetByIdAsync(execution.TaskId);
        if (task == null)
        {
            await FailRunAsync(execution, "Task definition not found.", null);
            return;
        }

        var snapshot = WorkflowDefinitionSerializer.Deserialize(execution.WorkflowSnapshotJson);
        if (snapshot == null)
        {
            await FailRunAsync(execution, "Workflow snapshot could not be deserialized.", null);
            return;
        }

        var ctx = WorkflowContext.Deserialize(execution.WorkflowStateJson);
        if (ctx == null)
        {
            ctx = new WorkflowContext();
            ctx.InitFrom(task, execution, task.DefaultInputsJson);
        }

        var currentNodeId = execution.CurrentNodeId ?? snapshot.StartNodeId;

        for (var step = 0; step < MaxNodeVisits; step++)
        {
            if (string.IsNullOrEmpty(currentNodeId))
            {
                await FailRunAsync(execution, "Run reached a null current node id.", task, ctx);
                return;
            }

            // Parallel branch-end detection: if the current node is the join target of
            // the top parallel frame, a branch has completed.
            if (ctx.Frames.Count > 0
                && ctx.Frames[^1] is { Type: "parallel" } pf
                && currentNodeId == pf.Next)
            {
                pf.Results ??= new List<bool>();
                pf.Results.Add(true);
                pf.BranchIndex++;
                if (pf.Branches != null && pf.BranchIndex < pf.Branches.Count)
                {
                    currentNodeId = pf.Branches[pf.BranchIndex];
                    continue;
                }

                // All branches done — join.
                var allSucceeded = pf.Results.Count > 0 && pf.Results.All(r => r);
                var joinMode = pf.JoinMode ?? nameof(WorkflowParallelJoinMode.AllSucceeded);
                if (joinMode == nameof(WorkflowParallelJoinMode.AllSucceeded) && !allSucceeded)
                {
                    await FailRunAsync(execution, "Parallel join failed: not all branches succeeded.", task, ctx);
                    return;
                }
                ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
                currentNodeId = pf.Next!;
                continue;
            }

            var def = snapshot.Nodes.FirstOrDefault(n => n.NodeId == currentNodeId);
            if (def == null)
            {
                await FailRunAsync(execution, $"Node '{currentNodeId}' not found in workflow snapshot.", task, ctx);
                return;
            }

            // Control-flow nodes (no persistent node-execution record).
            switch (def)
            {
                case StartWorkflowNode start:
                    currentNodeId = start.NextNodeId;
                    continue;
                case EndWorkflowNode end:
                    await CompleteRunAsync(execution, ctx, end);
                    return;
                case ConditionWorkflowNode cond:
                    currentNodeId = ctx.EvaluatePredicate(cond.Predicate) ? cond.TrueNodeId : cond.FalseNodeId;
                    continue;
                case ForEachWorkflowNode fe:
                    currentNodeId = HandleForEach(ctx, fe, out var feFailed, out var feError);
                    if (feFailed) { await FailRunAsync(execution, feError!, task, ctx); return; }
                    continue;
                case WhileWorkflowNode wh:
                    var (whNext, whFailed, whError) = HandleWhile(ctx, wh);
                    if (whFailed) { await FailRunAsync(execution, whError!, task, ctx); return; }
                    currentNodeId = whNext;
                    continue;
                case ParallelWorkflowNode par:
                    var frame = new WorkflowFrame
                    {
                        Type = "parallel",
                        NodeId = par.NodeId,
                        Branches = par.BranchStartNodeIds.ToList(),
                        BranchIndex = 0,
                        Results = new List<bool>(),
                        JoinMode = par.JoinMode.ToString(),
                        Next = par.NextNodeId
                    };
                    ctx.Frames.Add(frame);
                    if (frame.Branches.Count == 0)
                    {
                        ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
                        currentNodeId = par.NextNodeId;
                    }
                    else
                    {
                        currentNodeId = frame.Branches[0];
                    }
                    continue;
            }

            // Action node: ensure / resume / drive its node-execution record.
            var (next, paused, failed, failReason) = await ExecuteActionNodeAsync(
                execution, task, ctx, def, cancellationToken);
            if (paused)
            {
                execution.CurrentNodeId = def.NodeId;
                execution.WorkflowStateJson = ctx.Serialize();
                await _execRepo.UpdateAsync(execution);
                return;
            }
            if (failed)
            {
                await FailRunAsync(execution, $"Node '{def.NodeId}' failed: {failReason}", task, ctx);
                return;
            }

            currentNodeId = next;
            execution.CurrentNodeId = currentNodeId;
            execution.WorkflowStateJson = ctx.Serialize();
            await _execRepo.UpdateAsync(execution);
        }

        await FailRunAsync(execution, $"Run exceeded the maximum node-visit guard ({MaxNodeVisits}).", task, ctx);
    }

    private static string HandleForEach(WorkflowContext ctx, ForEachWorkflowNode fe, out bool failed, out string? error)
    {
        failed = false;
        error = null;

        if (ctx.Frames.Count > 0 && ctx.Frames[^1] is { Type: "for_each", NodeId: var fid } && fid == fe.NodeId)
        {
            var frame = ctx.Frames[^1];
            frame.Index++;
            if (frame.Items != null && frame.Index < frame.Items.Length && frame.Index < frame.MaxIter)
            {
                ctx.SetVariable(frame.ItemVariable ?? "item", frame.Items[frame.Index]);
                return frame.BodyStart!;
            }
            ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
            return frame.Next!;
        }

        var collectionNode = ctx.ResolvePath(fe.CollectionPath);
        if (collectionNode is not JsonArray items)
        {
            failed = true;
            error = $"for_each '{fe.NodeId}' collection path '{fe.CollectionPath}' did not resolve to an array.";
            return string.Empty;
        }

        var frameNew = new WorkflowFrame
        {
            Type = "for_each",
            NodeId = fe.NodeId,
            Items = items.Select(i => i?.DeepClone()).ToArray(),
            Index = 0,
            ItemVariable = fe.ItemVariable,
            BodyStart = fe.BodyStartNodeId,
            Next = fe.NextNodeId,
            MaxIter = fe.MaxIterations <= 0 ? items.Count : fe.MaxIterations
        };
        ctx.Frames.Add(frameNew);

        if (frameNew.Items.Length == 0 || frameNew.MaxIter <= 0)
        {
            ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
            return fe.NextNodeId;
        }

        ctx.SetVariable(frameNew.ItemVariable ?? "item", frameNew.Items[0]);
        return frameNew.BodyStart!;
    }

    private static (string Next, bool Failed, string? Error) HandleWhile(WorkflowContext ctx, WhileWorkflowNode wh)
    {
        if (ctx.Frames.Count > 0 && ctx.Frames[^1] is { Type: "while", NodeId: var wid } && wid == wh.NodeId)
        {
            var frame = ctx.Frames[^1];
            var predicateTrue = ctx.EvaluatePredicate(wh.Predicate);
            if (predicateTrue && frame.Count < frame.MaxIter)
            {
                frame.Count++;
                return (wh.BodyStartNodeId, false, null);
            }

            if (frame.Count >= frame.MaxIter && predicateTrue)
            {
                return (string.Empty, true, $"while '{wh.NodeId}' exceeded max iterations ({frame.MaxIter}).");
            }

            ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
            return (frame.Next!, false, null);
        }

        var maxIter = wh.MaxIterations <= 0 ? 1 : wh.MaxIterations;
        var frameNew = new WorkflowFrame
        {
            Type = "while",
            NodeId = wh.NodeId,
            Count = 0,
            BodyStart = wh.BodyStartNodeId,
            Next = wh.NextNodeId,
            MaxIter = maxIter
        };
        ctx.Frames.Add(frameNew);

        if (ctx.EvaluatePredicate(wh.Predicate) && frameNew.Count < frameNew.MaxIter)
        {
            frameNew.Count++;
            return (wh.BodyStartNodeId, false, null);
        }

        if (frameNew.Count >= frameNew.MaxIter && ctx.EvaluatePredicate(wh.Predicate))
        {
            return (string.Empty, true, $"while '{wh.NodeId}' exceeded max iterations ({frameNew.MaxIter}).");
        }

        ctx.Frames.RemoveAt(ctx.Frames.Count - 1);
        return (wh.NextNodeId, false, null);
    }

    private async Task<(string? Next, bool Paused, bool Failed, string? FailReason)> ExecuteActionNodeAsync(
        TaskExecution execution, ScheduledTask task, WorkflowContext ctx, WorkflowNode def, CancellationToken ct)
    {
        var scopeKey = ctx.ComputeScopeKey();
        var existing = await _execRepo.GetActiveNodeExecutionAsync(execution.Id, def.NodeId, scopeKey);

        WorkflowNodeExecution nodeExec;
        bool resuming;

        if (existing != null)
        {
            switch (existing.Status)
            {
                case WorkflowNodeStatus.Completed:
                    ctx.SetNodeOutput(def.NodeId, WorkflowDefinitionSerializer.ParseJsonNode(existing.OutputJson));
                    return (((LinearWorkflowNode)def).NextNodeId, false, false, null);

                case WorkflowNodeStatus.WaitingApproval:
                    return (null, true, false, null);

                case WorkflowNodeStatus.Retrying:
                    if (existing.NextRetryAt != null && existing.NextRetryAt > DateTime.UtcNow)
                    {
                        return (null, true, false, null);
                    }
                    existing.Attempt++;
                    existing.Status = WorkflowNodeStatus.Running;
                    existing.LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90);
                    await _execRepo.UpdateNodeExecutionAsync(existing);
                    nodeExec = existing;
                    resuming = true;
                    break;

                default:
                    // Pending/Running with no active driver — re-drive.
                    existing.Status = WorkflowNodeStatus.Running;
                    existing.LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90);
                    await _execRepo.UpdateNodeExecutionAsync(existing);
                    nodeExec = existing;
                    resuming = existing.Attempt > 0;
                    break;
            }
        }
        else
        {
            nodeExec = new WorkflowNodeExecution
            {
                ExecutionId = execution.Id,
                NodeId = def.NodeId,
                NodeType = WorkflowNodeExecutorDispatcher.GetNodeType(def),
                ScopeKey = scopeKey,
                Attempt = 0,
                Status = WorkflowNodeStatus.Running,
                StartedAt = DateTime.UtcNow,
                LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90)
            };
            await _execRepo.CreateNodeExecutionAsync(nodeExec);
            resuming = false;
        }

        await _execRepo.AddLogAsync(execution.Id, "Info",
            resuming ? $"Resuming node {def.NodeId} (attempt {nodeExec.Attempt})" : $"Executing node {def.NodeId}",
            $"scope={scopeKey}");

        NodeExecutionResult result;
        try
        {
            result = await _dispatcher.ExecuteAsync(nodeExec, def, ctx, execution, task, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Node executor threw for {NodeId}", def.NodeId);
            nodeExec.Status = WorkflowNodeStatus.Failed;
            nodeExec.FailureReason = ex.Message;
            nodeExec.CompletedAt = DateTime.UtcNow;
            nodeExec.LeaseExpiresAt = null;
            await _execRepo.UpdateNodeExecutionAsync(nodeExec);
            return (null, false, true, ex.Message);
        }

        await _execRepo.UpdateNodeExecutionAsync(nodeExec);

        if (result.Paused)
        {
            return (null, true, false, null);
        }

        if (result.Failed)
        {
            return (null, false, true, result.FailureReason);
        }

        // Completed.
        ctx.SetNodeOutput(def.NodeId, result.Output);
        return (result.NextNodeId, false, false, null);
    }

    private async Task CompleteRunAsync(TaskExecution execution, WorkflowContext ctx, EndWorkflowNode end)
    {
        if (end.ResultPath != null)
        {
            var result = ctx.ResolvePath(end.ResultPath);
            if (result != null)
            {
                ctx.SetVariable("result", result);
            }
        }

        execution.Status = Models.TaskStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        execution.CurrentNodeId = end.NodeId;
        execution.WorkflowStateJson = ctx.Serialize();
        await _execRepo.UpdateAsync(execution);
        await _execRepo.AddLogAsync(execution.Id, "Info", "Run completed");
        await _taskRepo.UpdateLastRunAsync(execution.TaskId, Models.TaskStatus.Completed);
    }

    private async Task FailRunAsync(TaskExecution execution, string message, ScheduledTask? task, WorkflowContext? ctx = null)
    {
        execution.Status = Models.TaskStatus.Failed;
        execution.ErrorMessage = message;
        execution.CompletedAt = DateTime.UtcNow;
        if (ctx != null) execution.WorkflowStateJson = ctx.Serialize();
        await _execRepo.UpdateAsync(execution);
        await _execRepo.AddLogAsync(execution.Id, "Error", message);
        if (task != null)
        {
            await _taskRepo.UpdateLastRunAsync(task.Id, Models.TaskStatus.Failed, message);
        }
    }

    public async Task ApproveNodeAsync(Guid executionId, Guid nodeExecutionId, bool approved, string? reason, CancellationToken cancellationToken = default)
    {
        var node = await _execRepo.GetWaitingApprovalNodeAsync(executionId, nodeExecutionId);
        if (node == null)
        {
            return;
        }

        if (approved)
        {
            node.Status = WorkflowNodeStatus.Completed;
            node.CompletedAt = DateTime.UtcNow;
            node.LeaseExpiresAt = null;
            await _execRepo.UpdateNodeExecutionAsync(node);
            await _execRepo.AddLogAsync(executionId, "Info", $"Node {node.NodeId} approved", reason);
            await ContinueRunAsync(executionId, cancellationToken);
        }
        else
        {
            node.Status = WorkflowNodeStatus.Failed;
            node.FailureReason = WorkflowFailureReason.ApprovalRejected;
            node.CompletedAt = DateTime.UtcNow;
            await _execRepo.UpdateNodeExecutionAsync(node);
            await _execRepo.AddLogAsync(executionId, "Warning", $"Node {node.NodeId} rejected", reason);

            var execution = await _execRepo.GetByIdAsync(executionId);
            if (execution != null)
            {
                // Resolve the task so FailRunAsync can propagate LastStatus / LastError
                // to the task row (Oracle gap: previously passed null, leaving the task
                // row reporting Pending even after a definitive rejection).
                var task = await _taskRepo.GetByIdAsync(execution.TaskId);
                await FailRunAsync(execution, $"Approval rejected for node '{node.NodeId}'.", task);
            }
        }
    }

    public async Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default)
    {
        var node = await _execRepo.GetNodeExecutionAsync(nodeExecutionId);
        return node != null && node.ExecutionId == executionId ? node : null;
    }

    public async Task<AgentExecutionSession?> GetNodeSessionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeExecutionAsync(executionId, nodeExecutionId, cancellationToken);
        if (node == null || node.AgentBackend == null) return null;

        var runtime = ResolveNodeRuntime(node, executionId);
        if (runtime == null) return null;
        var sessionGuid = DeterministicGuid.From(executionId.ToString(), node.NodeId, node.ScopeKey);
        return await runtime.GetExecutionSessionAsync(sessionGuid, cancellationToken);
    }

    public async Task<AgentExecutionSession> ResumeNodeSessionAsync(Guid executionId, Guid nodeExecutionId, string? sessionRef, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeExecutionAsync(executionId, nodeExecutionId, cancellationToken)
            ?? throw new InvalidOperationException("Node execution not found.");
        var runtime = ResolveNodeRuntime(node, executionId)
            ?? throw new InvalidOperationException("Node has no resolvable runtime.");
        var workingDir = node.AgentWorkingDirectory
            ?? throw new InvalidOperationException("Node has no working directory.");
        var sessionGuid = DeterministicGuid.From(executionId.ToString(), node.NodeId, node.ScopeKey);
        var reference = sessionRef ?? node.AgentSessionFile ?? node.AgentSessionId
            ?? throw new InvalidOperationException("Node has no session reference to resume.");

        var session = await runtime.ResumeExecutionSessionAsync(sessionGuid, workingDir, reference, _ => Task.CompletedTask, cancellationToken);
        node.AgentSessionId = session.SessionId;
        node.AgentSessionFile = session.SessionFile;
        await _execRepo.UpdateNodeExecutionAsync(node);
        return session;
    }

    public async Task<string> SendNodeMessageAsync(Guid executionId, Guid nodeExecutionId, string message, string mode, CancellationToken cancellationToken = default)
    {
        var node = await GetNodeExecutionAsync(executionId, nodeExecutionId, cancellationToken)
            ?? throw new InvalidOperationException("Node execution not found.");
        var runtime = ResolveNodeRuntime(node, executionId)
            ?? throw new InvalidOperationException("Node has no resolvable runtime.");
        var workingDir = node.AgentWorkingDirectory
            ?? throw new InvalidOperationException("Node has no working directory.");
        var sessionGuid = DeterministicGuid.From(executionId.ToString(), node.NodeId, node.ScopeKey);

        var agentMode = mode.ToLowerInvariant() switch
        {
            "prompt" => AgentMessageMode.Prompt,
            "steer" => AgentMessageMode.Steer,
            "followup" or "follow_up" => AgentMessageMode.FollowUp,
            _ => AgentMessageMode.Steer
        };

        return await runtime.SendMessageAsync(sessionGuid, workingDir, message, agentMode, _ => Task.CompletedTask, cancellationToken);
    }

    public async Task RecoverStuckNodesAsync(CancellationToken cancellationToken = default)
    {
        var running = await _execRepo.GetRunningNodeExecutionsAsync();
        var now = DateTime.UtcNow;
        foreach (var node in running)
        {
            if (node.LeaseExpiresAt == null || node.LeaseExpiresAt > now)
            {
                continue;
            }

            if (string.Equals(node.NodeType, "agent", StringComparison.OrdinalIgnoreCase))
            {
                var stuckExecution = await _execRepo.GetByIdAsync(node.ExecutionId);
                var task = stuckExecution != null ? await _taskRepo.GetByIdAsync(stuckExecution.TaskId) : null;
                var policy = task != null
                    ? WorkflowDefinitionSerializer.DeserializeFailurePolicy(task.NodeFailurePolicyJson)
                        ?? WorkflowDefinitionFactory.DefaultPiFailurePolicy()
                    : WorkflowNodeExecutorFactory.DefaultPolicy();

                if (node.Attempt + 1 < policy.MaxAttempts)
                {
                    node.Status = WorkflowNodeStatus.Retrying;
                    node.RetryCount = node.Attempt + 1;
                    node.NextRetryAt = now.AddSeconds(Math.Max(1, policy.RetryDelaySeconds));
                    node.LeaseExpiresAt = null;
                    await _execRepo.UpdateNodeExecutionAsync(node);
                    await _execRepo.AddLogAsync(node.ExecutionId, "Warning",
                        $"Recovered stuck agent node {node.NodeId}; scheduled retry", $"attempt={node.Attempt + 1}/{policy.MaxAttempts}");
                    continue;
                }
            }

            node.Status = WorkflowNodeStatus.Failed;
            node.FailureReason = WorkflowFailureReason.Timeout;
            node.CompletedAt = now;
            node.LeaseExpiresAt = null;
            await _execRepo.UpdateNodeExecutionAsync(node);
            await _execRepo.AddLogAsync(node.ExecutionId, "Error", $"Stuck node {node.NodeId} marked failed (expired lease)");

            var runExecution = await _execRepo.GetByIdAsync(node.ExecutionId);
            if (runExecution != null && runExecution.Status == Models.TaskStatus.Running)
            {
                // Resolve the task so FailRunAsync can propagate LastStatus / LastError
                // to the task row (Oracle gap: previously passed null, leaving the task
                // row reporting Pending even after a terminal lease-expiry failure).
                var failTask = await _taskRepo.GetByIdAsync(runExecution.TaskId);
                await FailRunAsync(runExecution, $"Node '{node.NodeId}' lease expired and was not recovered.", failTask);
            }
        }
    }

    private IAgentRuntime? ResolveNodeRuntime(WorkflowNodeExecution node, Guid executionId)
    {
        if (string.IsNullOrEmpty(node.AgentBackend)) return null;
        try
        {
            return _resolver.Get(node.AgentBackend);
        }
        catch
        {
            return null;
        }
    }
}

internal static class WorkflowNodeExecutorFactory
{
    public static WorkflowNodeFailurePolicy DefaultPolicy() => WorkflowDefinitionFactory.DefaultPiFailurePolicy();
}
