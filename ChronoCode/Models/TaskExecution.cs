using ChronoCode.Models.Workflow;

namespace ChronoCode.Models;

/// <summary>
/// One workflow run. Snapshot is frozen at creation. Node-level state lives in WorkflowNodeExecution.
/// </summary>
public class TaskExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TaskId { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Running;

    public int WorkflowVersion { get; set; } = 1;

    public string WorkflowSnapshotJson { get; set; } = "{}";

    public string? CurrentNodeId { get; set; }

    public string TriggerSource { get; set; } = WorkflowTriggerSource.Scheduled;

    public string? BranchName { get; set; }

    public string? CommitSha { get; set; }

    public string? PrUrl { get; set; }

    public int FilesChanged { get; set; }

    public string? ErrorMessage { get; set; }

    /// <summary>Serialized WorkflowRunState: execution stack + context, for resuming interrupted runs.</summary>
    public string? WorkflowStateJson { get; set; }

    public List<string> Logs { get; set; } = [];
}

public class TaskLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Level { get; set; } = "Info";

    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }
}
