namespace ChronoCode.Services;

public interface IChatRuntimeService
{
    Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default);
}

public class ChatRuntimeService : IChatRuntimeService
{
    private readonly IAgentRuntimeResolver _resolver;
    private readonly ILogger<ChatRuntimeService> _logger;

    public ChatRuntimeService(IAgentRuntimeResolver resolver, ILogger<ChatRuntimeService> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), "chronocode-chat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var executionId = Guid.NewGuid();
            var runtime = _resolver.Get(null);

            await runtime.EnsureReadyAsync(cancellationToken);

            await runtime.EnsureExecutionSessionAsync(
                executionId,
                workingDirectory,
                _ => Task.CompletedTask,
                null,
                cancellationToken);

            var prompt = BuildSystemPrompt(message);

            return await runtime.SendMessageAsync(
                executionId,
                workingDirectory,
                prompt,
                AgentMessageMode.Prompt,
                _ => Task.CompletedTask,
                cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up chat working directory {WorkingDirectory}", workingDirectory);
            }
        }
    }

    private static string BuildSystemPrompt(string message)
    {
        return $@"You are a task management assistant for ChronoCode. The user wants to manage scheduled tasks.

Available actions:
- create_task: Create a new scheduled task
- update_task: Update an existing task
- delete_task: Delete a task
- trigger_task: Manually trigger a task execution

User request: {message}

Respond ONLY with a JSON object in this format:
{{
  ""action"": ""create_task|update_task|delete_task|trigger_task"",
  ""task_id"": ""uuid if updating/deleting/triggering, null otherwise"",
  ""task"": {{
    ""name"": ""task name"",
    ""cron"": ""cron expression (e.g., 0 2 * * *)"",
    ""repository"": ""https://github.com/owner/repo"",
    ""base_branch"": ""main"",
    ""branch_strategy"": ""new|reuse"",
    ""max_runtime_seconds"": 600,
    ""max_file_changes"": 50,
    ""is_enabled"": true,
    ""workflow_definition_json"": ""a full node-graph workflow definition JSON string; default to the start -> prepare_workspace -> agent -> commit -> pr -> end shape"",
    ""default_inputs_json"": null,
    ""runtime_backend"": ""pi or null"",
    ""max_concurrent_runs"": 1,
    ""node_failure_policy_json"": ""retry policy JSON; default to {{}}""
  }}
}}

If the user just wants information or help, respond with a JSON containing:
{{
  ""action"": """",
  ""task"": null,
  ""error"": {{
    ""code"": ""INFO"",
    ""message"": ""your helpful response""
  }}
}}";
    }
}
