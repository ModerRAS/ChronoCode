using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>Executes a single workflow action node.</summary>
public interface INodeExecutor
{
    string NodeType { get; }

    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node,
        WorkflowNode def,
        WorkflowContext ctx,
        TaskExecution run,
        ScheduledTask task,
        CancellationToken ct);
}

/// <param name="NextNodeId">Next node id when completed (null when paused/failed).</param>
/// <param name="Output">Validated node output to store in context (null when paused/failed).</param>
/// <param name="Paused">True when the run should pause (approval / external retry).</param>
/// <param name="Failed">True when the node terminally failed.</param>
/// <param name="FailureReason">Reason string when failed.</param>
public sealed record NodeExecutionResult(
    string? NextNodeId,
    JsonNode? Output,
    bool Paused,
    bool Failed,
    string? FailureReason);

public sealed class WorkflowNodeExecutorDispatcher
{
    private readonly Dictionary<string, INodeExecutor> _byType;
    private readonly ILogger<WorkflowNodeExecutorDispatcher> _logger;

    public WorkflowNodeExecutorDispatcher(IEnumerable<INodeExecutor> executors, ILogger<WorkflowNodeExecutorDispatcher> logger)
    {
        _byType = executors.ToDictionary(e => e.NodeType, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node,
        WorkflowNode def,
        WorkflowContext ctx,
        TaskExecution run,
        ScheduledTask task,
        CancellationToken ct)
    {
        var type = GetNodeType(def);
        if (!_byType.TryGetValue(type, out var executor))
        {
            throw new InvalidOperationException($"No executor registered for node type '{type}'.");
        }
        node.NodeType = type;
        return await executor.ExecuteAsync(node, def, ctx, run, task, ct);
    }

    public static string GetNodeType(WorkflowNode def) => def switch
    {
        StartWorkflowNode => "start",
        PrepareWorkspaceWorkflowNode => "prepare_workspace",
        AgentWorkflowNode => "agent",
        ParallelWorkflowNode => "parallel",
        ConditionWorkflowNode => "condition",
        ForEachWorkflowNode => "for_each",
        WhileWorkflowNode => "while",
        ApprovalGateWorkflowNode => "approval_gate",
        CommitChangesWorkflowNode => "commit_changes",
        CreatePullRequestWorkflowNode => "create_pull_request",
        EndWorkflowNode => "end",
        _ => def.GetType().Name
    };
}

internal static class TemplateRenderer
{
    private static readonly Regex Token = new(@"\{\{\$(\.[^}]+)\}\}", RegexOptions.Compiled);

    public static string Render(string template, WorkflowContext ctx)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        return Token.Replace(template, m =>
        {
            var path = "$" + m.Groups[1].Value;
            var node = ctx.ResolvePath(path);
            if (node == null) return m.Value;
            if (node is JsonValue jv && jv.TryGetValue<string>(out var s)) return s;
            return node.ToJsonString();
        });
    }
}

internal static class DeterministicGuid
{
    public static Guid From(string a, string b, string c)
    {
        var bytes = Encoding.UTF8.GetBytes($"{a}|{b}|{c}");
        var hash = SHA256.HashData(bytes);
        var g = new byte[16];
        Array.Copy(hash, g, 16);
        return new Guid(g);
    }
}

public sealed class PrepareWorkspaceNodeExecutor : INodeExecutor
{
    private readonly IWorkspacePreparationService _preparation;
    public PrepareWorkspaceNodeExecutor(IWorkspacePreparationService preparation) => _preparation = preparation;
    public string NodeType => "prepare_workspace";

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node, WorkflowNode def, WorkflowContext ctx, TaskExecution run, ScheduledTask task, CancellationToken ct)
    {
        var result = await _preparation.PrepareAsync(task, run.Id, ct);
        var output = new JsonObject
        {
            ["workspacePath"] = result.WorkspacePath,
            ["branchName"] = result.BranchName
        };

        // Promote workspace/branch into the run context for downstream agent nodes.
        ctx.Run["workspacePath"] = result.WorkspacePath;
        ctx.Run["branchName"] = result.BranchName;
        run.BranchName = result.BranchName;

        node.OutputJson = output.ToJsonString();
        node.Status = WorkflowNodeStatus.Completed;
        node.CompletedAt = DateTime.UtcNow;
        return new NodeExecutionResult(((PrepareWorkspaceWorkflowNode)def).NextNodeId, output, false, false, null);
    }
}

public sealed class ApprovalGateNodeExecutor : INodeExecutor
{
    private readonly IExecutionRepository _execRepo;
    public ApprovalGateNodeExecutor(IExecutionRepository execRepo) => _execRepo = execRepo;
    public string NodeType => "approval_gate";

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node, WorkflowNode def, WorkflowContext ctx, TaskExecution run, ScheduledTask task, CancellationToken ct)
    {
        var gate = (ApprovalGateWorkflowNode)def;
        node.Status = WorkflowNodeStatus.WaitingApproval;
        node.LeaseExpiresAt = null;
        await _execRepo.AddLogAsync(run.Id, "Info", $"Approval required: {gate.Message}");
        return new NodeExecutionResult(null, null, true, false, null);
    }
}

public sealed class CommitChangesNodeExecutor : INodeExecutor
{
    private readonly IGitService _git;
    private readonly IExecutionRepository _execRepo;
    public CommitChangesNodeExecutor(IGitService git, IExecutionRepository execRepo)
    {
        _git = git;
        _execRepo = execRepo;
    }
    public string NodeType => "commit_changes";

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node, WorkflowNode def, WorkflowContext ctx, TaskExecution run, ScheduledTask task, CancellationToken ct)
    {
        var commitNode = (CommitChangesWorkflowNode)def;
        var workspacePath = ctx.Run["workspacePath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("commit_changes requires a prepared workspace (run.workspacePath).");

        var changedFiles = await _git.GetChangedFilesAsync(workspacePath);
        await _execRepo.AddLogAsync(run.Id, "Info", $"Changed {changedFiles.Count} files");

        if (changedFiles.Count > task.MaxFileChanges)
        {
            node.Status = WorkflowNodeStatus.Failed;
            node.FailureReason = $"too_many_files_changed:{changedFiles.Count}>{task.MaxFileChanges}";
            node.CompletedAt = DateTime.UtcNow;
            return new NodeExecutionResult(null, null, false, true, node.FailureReason);
        }

        var message = TemplateRenderer.Render(commitNode.CommitMessageTemplate, ctx);
        var commitSha = await _git.CommitChangesAsync(workspacePath, message);
        await _git.PushChangesAsync(workspacePath);
        await _execRepo.AddLogAsync(run.Id, "Info", $"Committed: {commitSha}");

        run.CommitSha = commitSha;
        run.FilesChanged = changedFiles.Count;
        ctx.Run["commitSha"] = commitSha;
        ctx.Run["filesChanged"] = changedFiles.Count;

        var output = new JsonObject { ["commitSha"] = commitSha, ["filesChanged"] = changedFiles.Count };
        node.OutputJson = output.ToJsonString();
        node.Status = WorkflowNodeStatus.Completed;
        node.CompletedAt = DateTime.UtcNow;
        return new NodeExecutionResult(commitNode.NextNodeId, output, false, false, null);
    }
}

public sealed class CreatePullRequestNodeExecutor : INodeExecutor
{
    private readonly IGitService _git;
    private readonly IExecutionRepository _execRepo;
    public CreatePullRequestNodeExecutor(IGitService git, IExecutionRepository execRepo)
    {
        _git = git;
        _execRepo = execRepo;
    }
    public string NodeType => "create_pull_request";

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node, WorkflowNode def, WorkflowContext ctx, TaskExecution run, ScheduledTask task, CancellationToken ct)
    {
        var prNode = (CreatePullRequestWorkflowNode)def;
        var workspacePath = ctx.Run["workspacePath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("create_pull_request requires a prepared workspace.");
        var branchName = ctx.Run["branchName"]?.GetValue<string>() ?? run.BranchName
            ?? throw new InvalidOperationException("create_pull_request requires a branch name.");

        var title = TemplateRenderer.Render(prNode.TitleTemplate, ctx);
        var body = TemplateRenderer.Render(prNode.BodyTemplate, ctx);
        var prUrl = await _git.CreatePullRequestAsync(workspacePath, branchName, task.BaseBranch, title, body);
        await _execRepo.AddLogAsync(run.Id, "Info", $"Pull request created: {prUrl}");

        run.PrUrl = prUrl;
        ctx.Run["prUrl"] = prUrl;

        var output = new JsonObject { ["prUrl"] = prUrl };
        node.OutputJson = output.ToJsonString();
        node.Status = WorkflowNodeStatus.Completed;
        node.CompletedAt = DateTime.UtcNow;
        return new NodeExecutionResult(prNode.NextNodeId, output, false, false, null);
    }
}
