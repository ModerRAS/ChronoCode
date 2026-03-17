using System.ComponentModel.DataAnnotations;

namespace ChronoCode.Models;

/// <summary>
/// Represents a scheduled task configuration
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

    [Required]
    public string Prompt { get; set; } = string.Empty;

    public int MaxRuntimeSeconds { get; set; } = 600;

    public int MaxFileChanges { get; set; } = 50;

    public bool RequirePlanReview { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastRunAt { get; set; }

    public TaskStatus LastStatus { get; set; } = TaskStatus.Pending;

    public bool IsEnabled { get; set; } = true;

    public string? LastError { get; set; }
}

/// <summary>
/// Branch creation strategy
/// </summary>
public enum BranchStrategy
{
    New,      // Create a new branch for each run
    Reuse     // Reuse existing branch
}

/// <summary>
/// Task execution status
/// </summary>
public enum TaskStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
