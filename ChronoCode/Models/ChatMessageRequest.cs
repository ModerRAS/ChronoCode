using ChronoCode.Models.DTOs;

namespace ChronoCode.Models;

public class ChatMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto> History { get; set; } = [];
}
