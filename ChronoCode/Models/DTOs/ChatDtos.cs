namespace ChronoCode.Models.DTOs;

public class SendChatMessageDto
{
    public string Message { get; set; } = string.Empty;
}

public class ChatMessageDto
{
    public Guid Id { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public class ChatConversationDto
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<ChatMessageDto> Messages { get; set; } = [];
}
