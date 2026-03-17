namespace ChronoCode.Models;

public class TaskExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Running;
    public string? BranchName { get; set; }
    public string? CommitSha { get; set; }
    public string? PrUrl { get; set; }
    public int FilesChanged { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Logs { get; set; } = new();
}

public class TaskLogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
