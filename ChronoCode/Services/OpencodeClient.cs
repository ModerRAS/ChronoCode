using System.Text;
using System.Text.Json;
using ChronoCode.Models;

namespace ChronoCode.Services;

public interface IOpencodeClient
{
    bool IsServerAvailable();
    Task<string> CreateSessionAsync(string workingDirectory, CancellationToken cancellationToken = default);
    Task<string> SendPromptAsync(string sessionId, string prompt, string workingDirectory, CancellationToken cancellationToken = default);
    Task<string> SendPromptWithStreamingAsync(string sessionId, string prompt, string workingDirectory, Func<string, Task> onChunk, CancellationToken cancellationToken = default);
    Task<List<FileDiff>> GetSessionDiffAsync(string sessionId, string? messageId = null, CancellationToken cancellationToken = default);
    Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<List<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default);
    Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public class OpencodeClient : IOpencodeClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOpencodeServerManager _serverManager;
    private readonly ILogger<OpencodeClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OpencodeClient(IHttpClientFactory httpClientFactory, IOpencodeServerManager serverManager, ILogger<OpencodeClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serverManager = serverManager;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    private string BaseUrl => _serverManager.ServerUrl;

    public bool IsServerAvailable()
    {
        return _serverManager.IsServerRunning;
    }

    private void AddDirectoryHeader(HttpRequestMessage request, string directory)
    {
        request.Headers.Add("x-opencode-directory", Uri.EscapeDataString(directory));
    }

    public async Task<string> CreateSessionAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/session");
        AddDirectoryHeader(request, workingDirectory);

        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<SessionInfo>(json, _jsonOptions);

        if (result == null)
            throw new Exception("Failed to create session");

        _logger.LogInformation("Created session {SessionId} in {Directory}", result.Id, workingDirectory);
        return result.Id;
    }

    public async Task<string> SendPromptAsync(string sessionId, string prompt, string workingDirectory, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/session/{sessionId}/message");
        AddDirectoryHeader(request, workingDirectory);

        var body = new
        {
            parts = new[]
            {
                new { type = "text", text = prompt }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<MessageResponse>(json, _jsonOptions);

        if (result?.Info == null)
            throw new Exception("Failed to get response");

        _logger.LogInformation("Received response for session {SessionId}", sessionId);
        return ExtractTextFromParts(result.Parts);
    }

    public async Task<string> SendPromptWithStreamingAsync(string sessionId, string prompt, string workingDirectory, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/session/{sessionId}/message");
        AddDirectoryHeader(request, workingDirectory);

        var body = new
        {
            parts = new[]
            {
                new { type = "text", text = prompt }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        var fullResponse = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null) break;
            if (!string.IsNullOrWhiteSpace(line))
            {
                await onChunk(line);
                fullResponse.AppendLine(line);
            }
        }

        return fullResponse.ToString();
    }

    public async Task<List<FileDiff>> GetSessionDiffAsync(string sessionId, string? messageId = null, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/session/{sessionId}/diff";
        if (!string.IsNullOrEmpty(messageId))
        {
            url += $"?messageID={messageId}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var diffs = JsonSerializer.Deserialize<List<FileDiff>>(json, _jsonOptions);

        return diffs ?? new List<FileDiff>();
    }

    public async Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/session/{sessionId}/abort");
        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/session");
        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var sessions = JsonSerializer.Deserialize<List<SessionInfo>>(json, _jsonOptions);

        return sessions ?? new List<SessionInfo>();
    }

    public async Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/session/{sessionId}");
        var response = await _httpClientFactory.CreateClient("Opencode").SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SessionInfo>(json, _jsonOptions);
    }

    private string ExtractTextFromParts(List<MessagePart> parts)
    {
        var textParts = parts
            .Where(p => p.Type == "text")
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrEmpty(t));

        return string.Join("\n", textParts);
    }
}

public class SessionInfo
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Agent { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MessageResponse
{
    public MessageInfo? Info { get; set; }
    public List<MessagePart> Parts { get; set; } = new();
}

public class MessageInfo
{
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class MessagePart
{
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public string? Path { get; set; }
    public string? Operation { get; set; }
}

public class FileDiff
{
    public string Path { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
}
