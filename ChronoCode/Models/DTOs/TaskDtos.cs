using System.ComponentModel.DataAnnotations;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Models.DTOs;

public class CreateTaskDto
{
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

    [Required]
    public string WorkflowDefinitionJson { get; set; } = "{}";

    public string? DefaultInputsJson { get; set; }

    public string? RuntimeBackend { get; set; }

    public int MaxConcurrentRuns { get; set; } = 1;

    public string NodeFailurePolicyJson { get; set; } = "{}";
}

public class UpdateTaskDto
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(50)]
    public string? CronExpression { get; set; }

    [MaxLength(500)]
    public string? RepositoryUrl { get; set; }

    [MaxLength(100)]
    public string? BaseBranch { get; set; }

    public BranchStrategy? BranchStrategy { get; set; }

    public int? MaxRuntimeSeconds { get; set; }

    public int? MaxFileChanges { get; set; }

    public bool? IsEnabled { get; set; }

    public string? WorkflowDefinitionJson { get; set; }

    public string? DefaultInputsJson { get; set; }

    public string? RuntimeBackend { get; set; }

    public int? MaxConcurrentRuns { get; set; }

    public string? NodeFailurePolicyJson { get; set; }
}

public class TaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public BranchStrategy BranchStrategy { get; set; }
    public int MaxRuntimeSeconds { get; set; }
    public int MaxFileChanges { get; set; }
    public bool IsEnabled { get; set; }
    public int WorkflowVersion { get; set; }
    public string WorkflowDefinitionJson { get; set; } = "{}";
    public string? DefaultInputsJson { get; set; }
    public string? RuntimeBackend { get; set; }
    public int MaxConcurrentRuns { get; set; }
    public string NodeFailurePolicyJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public TaskStatus LastStatus { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastQueuedAt { get; set; }
    public string SchedulerStatus { get; set; } = ChronoCode.Models.Workflow.SchedulerStatus.Idle;
    public DateTime? SchedulerHeartbeatAt { get; set; }
}

public class ExecutionDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TaskStatus Status { get; set; }
    public int WorkflowVersion { get; set; }
    public string? CurrentNodeId { get; set; }
    public string TriggerSource { get; set; } = WorkflowTriggerSource.Scheduled;
    public string? BranchName { get; set; }
    public string? CommitSha { get; set; }
    public string? PrUrl { get; set; }
    public int FilesChanged { get; set; }
    public string? ErrorMessage { get; set; }
}

public class NodeExecutionDto
{
    public Guid Id { get; set; }
    public Guid ExecutionId { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = string.Empty;
    public int Attempt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
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
}

public class ExecutionSessionDto
{
    public Guid ExecutionId { get; set; }
    public Guid NodeExecutionId { get; set; }
    public string? Backend { get; set; }
    public string? SessionId { get; set; }
    public string? SessionFile { get; set; }
    public string? WorkingDirectory { get; set; }
    public bool IsLive { get; set; }
    public bool SupportsPersistentSessions { get; set; }
    public bool SupportsSupplementalMessages { get; set; }
    public bool CanResume { get; set; }
}

public class ExecutionMessageDto
{
    [Required]
    public string Message { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Mode { get; set; } = "steer";
}

public class ResumeExecutionSessionDto
{
    [MaxLength(1024)]
    public string? SessionRef { get; set; }
}

public class ApprovalRequestDto
{
    public bool Approved { get; set; } = true;

    public string? Reason { get; set; }
}

public class LogDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class SchedulerQueueSnapshotDto
{
    public int NewRunItems { get; set; }
    public int NodeRetryItems { get; set; }
    public int ActiveRuns { get; set; }
    public List<QueueItemDto> Items { get; set; } = [];
}

public class QueueItemDto
{
    public string Kind { get; set; } = string.Empty;
    public Guid? ExecutionId { get; set; }
    public Guid? NodeExecutionId { get; set; }
    public Guid? TaskId { get; set; }
    public string? TaskName { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
}
