namespace ChronoCode.Models.DTOs;

using System.ComponentModel.DataAnnotations;

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

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public int MaxRuntimeSeconds { get; set; } = 600;

    public int MaxFileChanges { get; set; } = 50;

    public bool RequirePlanReview { get; set; } = true;

    public bool IsEnabled { get; set; } = true;
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

    public string? Prompt { get; set; }

    public int? MaxRuntimeSeconds { get; set; }

    public int? MaxFileChanges { get; set; }

    public bool? RequirePlanReview { get; set; }

    public bool? IsEnabled { get; set; }
}

public class TaskDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CronExpression { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = string.Empty;
    public BranchStrategy BranchStrategy { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public int MaxRuntimeSeconds { get; set; }
    public int MaxFileChanges { get; set; }
    public bool RequirePlanReview { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public TaskStatus LastStatus { get; set; }
    public bool IsEnabled { get; set; }
    public string? LastError { get; set; }
}

public class ExecutionDto
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TaskStatus Status { get; set; }
    public string? BranchName { get; set; }
    public string? CommitSha { get; set; }
    public string? PrUrl { get; set; }
    public int FilesChanged { get; set; }
    public string? ErrorMessage { get; set; }
}

public class LogDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
