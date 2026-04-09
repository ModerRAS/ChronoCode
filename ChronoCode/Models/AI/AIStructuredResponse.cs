namespace ChronoCode.Models.AI;

/// <summary>
/// DTO for AI-structured task creation request
/// Uses snake_case field names to match what AI returns
/// </summary>
public class AIStructuredResponse
{
    public string Action { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public AITaskDto? Task { get; set; }
    public AIError? Error { get; set; }
}

/// <summary>
/// Task DTO with snake_case fields (as returned by AI)
/// </summary>
public class AITaskDto
{
    public string Name { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = "main";
    public string BranchStrategy { get; set; } = "New";
    public string Prompt { get; set; } = string.Empty;
    public int MaxRuntimeSeconds { get; set; } = 600;
    public int MaxFileChanges { get; set; } = 50;
    public bool RequirePlanReview { get; set; } = true;
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Convert AI response DTO to CreateTaskDto
    /// </summary>
    public DTOs.CreateTaskDto ToCreateTaskDto()
    {
        return new DTOs.CreateTaskDto
        {
            Name = Name,
            CronExpression = Cron,
            RepositoryUrl = Repository,
            BaseBranch = BaseBranch,
            BranchStrategy = BranchStrategy?.ToLower() == "reuse" 
                ? Models.BranchStrategy.Reuse 
                : Models.BranchStrategy.New,
            Prompt = Prompt,
            MaxRuntimeSeconds = MaxRuntimeSeconds,
            MaxFileChanges = MaxFileChanges,
            RequirePlanReview = RequirePlanReview,
            IsEnabled = IsEnabled
        };
    }
}

/// <summary>
/// Error info returned by AI
/// </summary>
public class AIError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Action constants for AI responses
/// </summary>
public static class AIActions
{
    public const string CreateTask = "create_task";
    public const string UpdateTask = "update_task";
    public const string DeleteTask = "delete_task";
    public const string TriggerTask = "trigger_task";

    public static readonly string[] ValidActions = { CreateTask, UpdateTask, DeleteTask, TriggerTask };

    public static bool IsValid(string action) => ValidActions.Contains(action);
}
