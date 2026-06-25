using System.ComponentModel.DataAnnotations;

namespace ChronoCode.Models;

/// <summary>
/// A persisted AI chat conversation. The pi agent session file/id is stored so the
/// same session can be resumed for follow-up messages.
/// </summary>
public class ChatConversation
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string? Title { get; set; }

    [MaxLength(1024)]
    public string? AgentSessionFile { get; set; }

    [MaxLength(256)]
    public string? AgentSessionId { get; set; }

    [MaxLength(1024)]
    public string WorkingDirectory { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatMessage> Messages { get; set; } = [];
}

/// <summary>
/// One message in a chat conversation. Stored as a mirror of what the pi agent session owns.
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ConversationId { get; set; }

    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
