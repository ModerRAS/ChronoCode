using ChronoCode.Models.DTOs;

namespace ChronoCode.Models.AI;

public class AIStructuredResponse
{
    public string Action { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public CreateTaskDto? Task { get; set; }
    public AIError? Error { get; set; }
}

public class AIError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public static class AIActions
{
    public const string CreateTask = "create_task";
    public const string UpdateTask = "update_task";
    public const string DeleteTask = "delete_task";
    public const string TriggerTask = "trigger_task";

    public static readonly string[] ValidActions = { CreateTask, UpdateTask, DeleteTask, TriggerTask };

    public static bool IsValid(string action) => ValidActions.Contains(action);
}
