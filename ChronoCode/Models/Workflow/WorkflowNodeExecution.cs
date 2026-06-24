namespace ChronoCode.Models.Workflow;

public class WorkflowNodeExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExecutionId { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string ScopeKey { get; set; } = string.Empty;

    public int Attempt { get; set; }

    public string Status { get; set; } = WorkflowNodeStatus.Pending;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public string? InputJson { get; set; }

    public string? OutputJson { get; set; }

    public string? ValidationError { get; set; }

    public string? AgentBackend { get; set; }

    public string? AgentSessionId { get; set; }

    public string? AgentSessionFile { get; set; }

    public string? AgentWorkingDirectory { get; set; }

    public string? FailureReason { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public int RetryCount { get; set; }

    public DateTime? LeaseExpiresAt { get; set; }

    public bool SchemaRepairAttempted { get; set; }
}
