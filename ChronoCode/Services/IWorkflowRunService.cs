using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services;

/// <summary>
/// Single entry point for workflow run lifecycle: create run + frozen snapshot,
/// dispatch node executors, persist run/node state, resume interrupted runs,
/// and recover stuck nodes (expired leases).
/// </summary>
public interface IWorkflowRunService
{
    Task<TaskExecution> StartRunAsync(ScheduledTask task, string triggerSource, CancellationToken cancellationToken = default);

    /// <summary>Advance an existing run: process due/retrying nodes, resume paused runs, complete finished runs.</summary>
    Task ContinueRunAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task ApproveNodeAsync(Guid executionId, Guid nodeExecutionId, bool approved, string? reason, CancellationToken cancellationToken = default);

    Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default);

    Task<AgentExecutionSession?> GetNodeSessionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default);

    Task<AgentExecutionSession> ResumeNodeSessionAsync(Guid executionId, Guid nodeExecutionId, string? sessionRef, CancellationToken cancellationToken = default);

    Task<string> SendNodeMessageAsync(Guid executionId, Guid nodeExecutionId, string message, string mode, CancellationToken cancellationToken = default);

    /// <summary>Scan running agent nodes with expired leases; flip them to retrying/failed. Never leaves a run stuck in running.</summary>
    Task RecoverStuckNodesAsync(CancellationToken cancellationToken = default);
}
