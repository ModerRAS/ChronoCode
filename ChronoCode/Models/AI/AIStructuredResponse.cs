using System.Text.Json.Serialization;
using ChronoCode.Models.DTOs;

namespace ChronoCode.Models.AI;

/// <summary>
/// DTO for AI-structured task management request.
/// snake_case field names match the AI model output. The task payload now carries
/// a workflow definition instead of a single prompt.
/// </summary>
public class AIStructuredResponse
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("task_id")]
    public Guid? TaskId { get; set; }

    [JsonPropertyName("task")]
    public AITaskDto? Task { get; set; }

    [JsonPropertyName("error")]
    public AIError? Error { get; set; }
}

public class AITaskDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("cron")]
    public string Cron { get; set; } = string.Empty;

    [JsonPropertyName("repository")]
    public string Repository { get; set; } = string.Empty;

    [JsonPropertyName("base_branch")]
    public string BaseBranch { get; set; } = "main";

    [JsonPropertyName("branch_strategy")]
    public string BranchStrategy { get; set; } = "new";

    [JsonPropertyName("max_runtime_seconds")]
    public int MaxRuntimeSeconds { get; set; } = 600;

    [JsonPropertyName("max_file_changes")]
    public int MaxFileChanges { get; set; } = 50;

    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("workflow_definition_json")]
    public string? WorkflowDefinitionJson { get; set; }

    [JsonPropertyName("default_inputs_json")]
    public string? DefaultInputsJson { get; set; }

    [JsonPropertyName("runtime_backend")]
    public string? RuntimeBackend { get; set; }

    [JsonPropertyName("max_concurrent_runs")]
    public int MaxConcurrentRuns { get; set; } = 1;

    [JsonPropertyName("node_failure_policy_json")]
    public string? NodeFailurePolicyJson { get; set; }

    public CreateTaskDto ToCreateTaskDto()
    {
        var workflowJson = string.IsNullOrWhiteSpace(WorkflowDefinitionJson)
        ? Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null)
            : WorkflowDefinitionJson!;

        var branchStrategy = string.Equals(BranchStrategy, "reuse", StringComparison.OrdinalIgnoreCase)
            ? Models.BranchStrategy.Reuse
            : Models.BranchStrategy.New;

        return new CreateTaskDto
        {
            Name = Name,
            CronExpression = Cron,
            RepositoryUrl = Repository,
            BaseBranch = BaseBranch,
            BranchStrategy = branchStrategy,
            MaxRuntimeSeconds = MaxRuntimeSeconds,
            MaxFileChanges = MaxFileChanges,
            IsEnabled = IsEnabled,
            WorkflowDefinitionJson = workflowJson,
            DefaultInputsJson = DefaultInputsJson,
            RuntimeBackend = RuntimeBackend,
            MaxConcurrentRuns = MaxConcurrentRuns,
            NodeFailurePolicyJson = string.IsNullOrWhiteSpace(NodeFailurePolicyJson)
                ? Workflow.WorkflowDefinitionFactory.DefaultPiFailurePolicyJson()
                : NodeFailurePolicyJson!
        };
    }
}

public class AIError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public static class AIActions
{
    public const string CreateTask = "create_task";
    public const string UpdateTask = "update_task";
    public const string DeleteTask = "delete_task";
    public const string TriggerTask = "trigger_task";

    public static bool IsValid(string? action) =>
        action == CreateTask || action == UpdateTask || action == DeleteTask || action == TriggerTask;
}
