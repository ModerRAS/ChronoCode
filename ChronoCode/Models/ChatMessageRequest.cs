namespace ChronoCode.Models;

public class ChatMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessage> History { get; set; } = [];
}

public class ChatMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
