using System.Collections.Concurrent;
using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

public interface IChatRuntimeService
{
    Task<ChatConversationDto> CreateConversationAsync(CancellationToken cancellationToken = default);

    Task<ChatConversationDto?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task<ChatMessageDto> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default);

    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
}

public class ChatRuntimeService : IChatRuntimeService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _conversationLocks = new();
    private readonly IAgentRuntimeResolver _resolver;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ChatRuntimeService> _logger;
    private bool _disposed;

    public ChatRuntimeService(
        IAgentRuntimeResolver resolver,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<ChatRuntimeService> logger)
    {
        _resolver = resolver;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task<ChatConversationDto> CreateConversationAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();

        var workingDirectory = Path.Combine(Path.GetTempPath(), "chronocode-chat", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        var conversation = new ChatConversation
        {
            Title = "New Chat",
            WorkingDirectory = workingDirectory,
        };

        db.ChatConversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);

        return MapConversation(conversation);
    }

    public async Task<ChatConversationDto?> GetConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();

        var conversation = await db.ChatConversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        return conversation == null ? null : MapConversation(conversation);
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        var lockObj = _conversationLocks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(cancellationToken);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();

            var conversation = await db.ChatConversations
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

            if (conversation == null)
            {
                throw new InvalidOperationException($"Conversation {conversationId} not found.");
            }

            Directory.CreateDirectory(conversation.WorkingDirectory);

            var runtime = _resolver.Get(null);
            await runtime.EnsureReadyAsync(cancellationToken);

            var session = await GetOrResumeSessionAsync(conversation, runtime, cancellationToken);

            // Persist the user message before sending it to the agent.
            var userMessage = new ChatMessage
            {
                ConversationId = conversationId,
                Role = "user",
                Content = message,
                CreatedAt = DateTime.UtcNow,
            };
            db.ChatMessages.Add(userMessage);
            await db.SaveChangesAsync(cancellationToken);

            var hasAssistantMessages = conversation.Messages.Any(m => m.Role == "ai");
            var mode = hasAssistantMessages ? AgentMessageMode.FollowUp : AgentMessageMode.Prompt;

            var response = await runtime.SendMessageAsync(
                conversationId,
                conversation.WorkingDirectory,
                message,
                mode,
                _ => Task.CompletedTask,
                cancellationToken);

            var assistantMessage = new ChatMessage
            {
                ConversationId = conversationId,
                Role = "ai",
                Content = response,
                CreatedAt = DateTime.UtcNow,
            };
            db.ChatMessages.Add(assistantMessage);

            conversation.AgentSessionId = session.SessionId;
            conversation.AgentSessionFile = session.SessionFile;
            conversation.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                await runtime.StopExecutionAsync(conversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop chat runtime execution {ConversationId}", conversationId);
            }

            return MapMessage(assistantMessage);
        }
        finally
        {
            lockObj.Release();
        }
    }

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var lockObj = _conversationLocks.GetOrAdd(conversationId, _ => new SemaphoreSlim(1, 1));
        await lockObj.WaitAsync(cancellationToken);

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChronoDbContext>();

            var conversation = await db.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

            if (conversation == null)
            {
                return;
            }

            var runtime = _resolver.Get(null);
            try
            {
                await runtime.StopExecutionAsync(conversationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "No active chat runtime to stop for {ConversationId}", conversationId);
            }

            db.ChatConversations.Remove(conversation);
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                Directory.Delete(conversation.WorkingDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up chat working directory {WorkingDirectory}", conversation.WorkingDirectory);
            }
        }
        finally
        {
            lockObj.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        var runtime = _resolver.Get(null);
        foreach (var conversationId in _conversationLocks.Keys.ToList())
        {
            try
            {
                runtime.StopExecutionAsync(conversationId).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ignoring stop failure during disposal");
            }

            if (_conversationLocks.TryRemove(conversationId, out var lockObj))
            {
                lockObj.Dispose();
            }
        }
    }

    private async Task<AgentExecutionSession> GetOrResumeSessionAsync(
        ChatConversation conversation,
        IAgentRuntime runtime,
        CancellationToken cancellationToken)
    {
        var sessionRef = conversation.AgentSessionFile ?? conversation.AgentSessionId;
        if (!string.IsNullOrWhiteSpace(sessionRef))
        {
            try
            {
                return await runtime.ResumeExecutionSessionAsync(
                    conversation.Id,
                    conversation.WorkingDirectory,
                    sessionRef,
                    _ => Task.CompletedTask,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resume chat session {SessionRef}; starting fresh.", sessionRef);
            }
        }

        return await runtime.EnsureExecutionSessionAsync(
            conversation.Id,
            conversation.WorkingDirectory,
            _ => Task.CompletedTask,
            null,
            cancellationToken);
    }

    private static ChatConversationDto MapConversation(ChatConversation conversation)
    {
        return new ChatConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = conversation.UpdatedAt,
            Messages = conversation.Messages
                .OrderBy(m => m.CreatedAt)
                .Select(MapMessage)
                .ToList(),
        };
    }

    private static ChatMessageDto MapMessage(ChatMessage message)
    {
        return new ChatMessageDto
        {
            Id = message.Id,
            Role = message.Role,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
        };
    }
}

