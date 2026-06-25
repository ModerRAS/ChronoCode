using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>
/// Executes a workflow <c>agent</c> node against the pi runtime: ensures/resumes a
/// stable per-visit session, sends the rendered prompt, validates the output envelope
/// against the data contract (one in-session schema-repair attempt), and applies the
/// node failure policy on runtime exceptions.
/// </summary>
public sealed class AgentNodeExecutor : INodeExecutor
{
    private readonly IAgentRuntimeResolver _resolver;
    private readonly IExecutionRepository _execRepo;
    private readonly ILogger<AgentNodeExecutor> _logger;

    public AgentNodeExecutor(IAgentRuntimeResolver resolver, IExecutionRepository execRepo, ILogger<AgentNodeExecutor> logger)
    {
        _resolver = resolver;
        _execRepo = execRepo;
        _logger = logger;
    }

    public string NodeType => "agent";

    public async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNodeExecution node,
        WorkflowNode def,
        WorkflowContext ctx,
        TaskExecution run,
        ScheduledTask task,
        CancellationToken ct)
    {
        var agentNode = (AgentWorkflowNode)def;
        var now = DateTime.UtcNow;

        var backend = agentNode.Backend ?? task.RuntimeBackend ?? WorkflowBackend.Pi;
        if (!string.Equals(backend, WorkflowBackend.Pi, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Workflow agent nodes require the 'pi' backend; opencode is not permitted.");
        }

        var runtime = _resolver.Get(backend);
        var workingDir = ctx.Run["workspacePath"]?.GetValue<string>()
            ?? throw new InvalidOperationException("agent node requires a prepared workspace (run.workspacePath).");

        var sessionGuid = DeterministicGuid.From(run.Id.ToString(), node.NodeId, node.ScopeKey);
        node.AgentBackend = backend;
        node.AgentWorkingDirectory = workingDir;
        node.LeaseExpiresAt = now.AddSeconds(90);

        var policy = agentNode.FailurePolicy
            ?? WorkflowDefinitionSerializer.DeserializeFailurePolicy(task.NodeFailurePolicyJson)
            ?? WorkflowDefinitionFactory.DefaultPiFailurePolicy();

        Func<string, Task> onChunk = chunk => _execRepo.AddLogAsync(run.Id, "Debug", chunk);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, task.MaxRuntimeSeconds)));

        try
        {
            // Ensure / resume a persistent session keyed by the stable per-visit guid.
            var sessionRef = node.AgentSessionFile ?? node.AgentSessionId;
            AgentExecutionSession session;
            if (!string.IsNullOrEmpty(sessionRef) && policy.ResumeSession)
            {
                await _execRepo.AddLogAsync(run.Id, "Info", "Resuming agent session", sessionRef);
                session = await runtime.ResumeExecutionSessionAsync(sessionGuid, workingDir, sessionRef, onChunk, timeoutCts.Token);
            }
            else
            {
                session = await runtime.EnsureExecutionSessionAsync(sessionGuid, workingDir, onChunk, null, timeoutCts.Token);
            }

            node.AgentSessionId = session.SessionId;
            node.AgentSessionFile = session.SessionFile;
            node.AgentWorkingDirectory = session.WorkingDirectory;
            await _execRepo.UpdateNodeExecutionAsync(node);

            // Renew lease while waiting for the model to respond.
            node.LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90);

            var prompt = TemplateRenderer.Render(agentNode.PromptTemplate, ctx);
            await _execRepo.AddLogAsync(run.Id, "Info", "Sending agent prompt");
            var rawResponse = await runtime.SendMessageAsync(
                sessionGuid, workingDir, prompt, AgentMessageMode.Prompt, onChunk, timeoutCts.Token);

            // Validate envelope + data contract.
            if (AgentOutputValidator.ValidateAgentOutput(rawResponse, agentNode.DataContract, out var envelope, out var error))
            {
                node.OutputJson = envelope.ToJsonString();
                node.Status = WorkflowNodeStatus.Completed;
                node.CompletedAt = DateTime.UtcNow;
                node.ValidationError = null;
                node.LeaseExpiresAt = null;
                return new NodeExecutionResult(agentNode.NextNodeId, envelope, false, false, null);
            }

            // First validation failure: one in-session schema-repair attempt.
            if (!node.SchemaRepairAttempted)
            {
                node.SchemaRepairAttempted = true;
                node.ValidationError = error;
                node.LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90);
                await _execRepo.UpdateNodeExecutionAsync(node);
                await _execRepo.AddLogAsync(run.Id, "Warning", "Agent output invalid; requesting schema repair", error);

                var repairPrompt = BuildSchemaRepairPrompt(agentNode.DataContract, error);
                var repairResponse = await runtime.SendMessageAsync(
                    sessionGuid, workingDir, repairPrompt, AgentMessageMode.FollowUp, onChunk, timeoutCts.Token);

                if (AgentOutputValidator.ValidateAgentOutput(repairResponse, agentNode.DataContract, out var repaired, out var error2))
                {
                    node.OutputJson = repaired.ToJsonString();
                    node.Status = WorkflowNodeStatus.Completed;
                    node.ValidationError = null;
                    node.CompletedAt = DateTime.UtcNow;
                    node.LeaseExpiresAt = null;
                    return new NodeExecutionResult(agentNode.NextNodeId, repaired, false, false, null);
                }

                node.ValidationError = error2;
                node.Status = WorkflowNodeStatus.SchemaValidationFailed;
                node.FailureReason = WorkflowFailureReason.SchemaValidationFailed;
                node.CompletedAt = DateTime.UtcNow;
                node.LeaseExpiresAt = null;
                await _execRepo.AddLogAsync(run.Id, "Error", "Schema repair failed; node terminated", error2);
                return new NodeExecutionResult(null, null, false, true, WorkflowFailureReason.SchemaValidationFailed);
            }

            // Schema repair already attempted previously -> terminal failure.
            node.ValidationError = error;
            node.Status = WorkflowNodeStatus.SchemaValidationFailed;
            node.FailureReason = WorkflowFailureReason.SchemaValidationFailed;
            node.CompletedAt = DateTime.UtcNow;
            node.LeaseExpiresAt = null;
            return new NodeExecutionResult(null, null, false, true, WorkflowFailureReason.SchemaValidationFailed);
        }
        catch (Exception ex)
        {
            return await HandleExceptionAsync(node, ex, policy, run.Id);
        }
    }

    private async Task<NodeExecutionResult> HandleExceptionAsync(
        WorkflowNodeExecution node, Exception ex, WorkflowNodeFailurePolicy policy, Guid executionId)
    {
        var reason = FailureClassifier.Classify(ex);
        _logger.LogWarning(ex, "Agent node {NodeId} failed: {Reason}", node.NodeId, reason);

        if (reason != null
            && policy.RetryOn.Contains(reason.Value)
            && node.Attempt + 1 < policy.MaxAttempts)
        {
            node.Status = WorkflowNodeStatus.Retrying;
            node.RetryCount = node.Attempt + 1;
            node.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Max(1, policy.RetryDelaySeconds));
            node.LeaseExpiresAt = null;
            await _execRepo.AddLogAsync(
                executionId, "Warning",
                $"Agent node failed ({reason}); will retry", $"attempt={node.Attempt + 1}/{policy.MaxAttempts}; {ex.Message}");
            // Persist session ref so the next attempt resumes the same session.
            return new NodeExecutionResult(null, null, true, false, null);
        }

        node.Status = WorkflowNodeStatus.Failed;
        node.FailureReason = reason?.ToString() ?? WorkflowFailureReason.MaxAttemptsExceeded;
        node.CompletedAt = DateTime.UtcNow;
        node.LeaseExpiresAt = null;
        await _execRepo.AddLogAsync(
            executionId, "Error",
            $"Agent node terminally failed: {node.FailureReason}", ex.Message);
        return new NodeExecutionResult(null, null, false, true, node.FailureReason);
    }

    private static string BuildSchemaRepairPrompt(WorkflowDataContract contract, string error)
    {
        var fields = contract?.Fields != null && contract.Fields.Count > 0
            ? string.Join("\n", contract.Fields.Select(f =>
                $"- {f.Name}: {f.Type.ToLowerString()}{(f.Required ? " (required)" : "")}"))
            : "(no fields declared)";

        return $@"
Your previous reply did not match the required JSON envelope and was rejected.

REASON: {error}

You MUST reply with ONLY a JSON object (no prose, no markdown fences) of the shape:
{{
  ""status"": ""completed"" | ""blocked"" | ""failed"",
  ""passed"": true | false | null,
  ""summary"": ""short human-readable summary"",
  ""artifacts"": [""relative/path/to/file"", ...],
  ""data"": {{ ... agreed structured fields ... }}
}}

The 'data' object must satisfy this contract:
{fields}

Reply now with ONLY the envelope JSON.";
    }
}

internal static class WorkflowDataTypeExtensions
{
    public static string ToLowerString(this WorkflowDataType t) => t.ToString().ToLowerInvariant();
}
