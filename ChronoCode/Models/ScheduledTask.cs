using System.ComponentModel.DataAnnotations;

namespace ChronoCode.Models;

/// <summary>
/// A scheduled workflow task definition: top-level scheduling/repo/runtime fields
/// plus a persisted workflow graph DSL (WorkflowDefinitionJson).
/// </summary>
public class ScheduledTask
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string CronExpression { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string RepositoryUrl { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BaseBranch { get; set; } = "main";

    public BranchStrategy BranchStrategy { get; set; } = BranchStrategy.New;

    public int MaxRuntimeSeconds { get; set; } = 600;

    public int MaxFileChanges { get; set; } = 50;

    public bool IsEnabled { get; set; } = true;

    public int WorkflowVersion { get; set; } = 1;

    [Required]
    public string WorkflowDefinitionJson { get; set; } = "{}";

    public string? DefaultInputsJson { get; set; }

    /// <summary>"pi" | null. opencode is forbidden for workflow agent nodes.</summary>
    [MaxLength(32)]
    public string? RuntimeBackend { get; set; }

    public int MaxConcurrentRuns { get; set; } = 1;

    public string NodeFailurePolicyJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastRunAt { get; set; }

    public TaskStatus LastStatus { get; set; } = TaskStatus.Pending;

    public string? LastError { get; set; }

    public DateTime? NextRunAt { get; set; }

    public DateTime? LastQueuedAt { get; set; }

    public string SchedulerStatus { get; set; } = Workflow.SchedulerStatus.Idle;

    public DateTime? SchedulerHeartbeatAt { get; set; }
}

/// <summary>Branch creation strategy</summary>
public enum BranchStrategy
{
    New,
    Reuse
}

/// <summary>Workflow run status (reused for task-level last status)</summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
