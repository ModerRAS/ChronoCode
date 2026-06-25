using ChronoCode.Models;
using System.Text;

namespace ChronoCode.Services;

public interface IChatRuntimeService
{
    Task<string> SendChatMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default);
}

public class ChatRuntimeService : IChatRuntimeService
{
    private const int MaxHistoryMessages = 20;
    private readonly IAgentRuntimeResolver _resolver;
    private readonly ILogger<ChatRuntimeService> _logger;

    public ChatRuntimeService(IAgentRuntimeResolver resolver, ILogger<ChatRuntimeService> logger)
    {
        _resolver = resolver;
        _logger = logger;
    }

    public async Task<string> SendChatMessageAsync(
        string message,
        List<ChatMessage>? history = null,
        CancellationToken cancellationToken = default)
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

            var prompt = BuildPrompt(message, history);

            var response = await runtime.SendMessageAsync(
                executionId,
                workingDirectory,
                prompt,
                AgentMessageMode.Prompt,
                _ => Task.CompletedTask,
                cancellationToken);

            try
            {
                await runtime.StopExecutionAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop chat runtime execution {ExecutionId}", executionId);
            }

            return response;
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

    private static string BuildPrompt(string message, List<ChatMessage>? history)
    {
        var promptBuilder = new StringBuilder();
        if (history is { Count: > 0 })
        {
            promptBuilder.AppendLine("Previous conversation:");
            foreach (var item in history.TakeLast(MaxHistoryMessages))
            {
                var label = item.Role switch
                {
                    "user" => "User",
                    "ai" => "Assistant",
                    _ => item.Role,
                };
                promptBuilder.AppendLine($"{label}: {item.Content}");
            }
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine($"User: {message}");
        return promptBuilder.ToString();
    }
}
