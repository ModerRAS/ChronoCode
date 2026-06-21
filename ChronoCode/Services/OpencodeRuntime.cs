namespace ChronoCode.Services;

public class OpencodeRuntime : IAgentRuntime
{
    private readonly IOpencodeClient _client;
    private readonly IOpencodeServerManager _serverManager;
    private readonly Dictionary<Guid, AgentExecutionSession> _sessions = new();

    public OpencodeRuntime(IOpencodeClient client, IOpencodeServerManager serverManager)
    {
        _client = client;
        _serverManager = serverManager;
    }

    public AgentRuntimeStatus GetStatus()
    {
        return new AgentRuntimeStatus(
            Backend: "opencode",
            IsReady: _serverManager.IsServerRunning,
            Endpoint: _serverManager.ServerUrl,
            SupportsLifecycleControls: true,
            SupportsPersistentSessions: false,
            SupportsSupplementalMessages: false);
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_serverManager.IsServerRunning)
        {
            return;
        }

        await _serverManager.StartServerAsync(cancellationToken);
        await _serverManager.WaitForServerReadyAsync(TimeSpan.FromSeconds(30));
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _serverManager.StopServerAsync();
    }

    public async Task<AgentExecutionSession> EnsureExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        Func<string, Task> onChunk,
        string? sessionRef = null,
        CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(executionId, out var existing))
        {
            return existing;
        }

        await EnsureReadyAsync(cancellationToken);
        var sessionId = await _client.CreateSessionAsync(workingDirectory, cancellationToken);
        var session = new AgentExecutionSession(
            Backend: "opencode",
            SessionId: sessionId,
            SessionFile: null,
            WorkingDirectory: workingDirectory,
            SupportsSupplementalMessages: false);
        _sessions[executionId] = session;
        return session;
    }

    public async Task<string> SendMessageAsync(
        Guid executionId,
        string workingDirectory,
        string prompt,
        AgentMessageMode mode,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (mode != AgentMessageMode.Prompt)
        {
            throw new NotSupportedException("Opencode runtime does not support steer/follow-up messages.");
        }

        var session = await EnsureExecutionSessionAsync(executionId, workingDirectory, onChunk, null, cancellationToken);
        return await _client.SendPromptWithStreamingAsync(session.SessionId!, prompt, workingDirectory, onChunk, cancellationToken);
    }

    public Task<AgentExecutionSession?> GetExecutionSessionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        _sessions.TryGetValue(executionId, out var session);
        return Task.FromResult(session);
    }

    public Task<AgentExecutionSession> ResumeExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        string sessionRef,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Opencode runtime does not support persistent session resume.");
    }

    public async Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(executionId, out var session) && !string.IsNullOrWhiteSpace(session.SessionId))
        {
            await _client.AbortSessionAsync(session.SessionId, cancellationToken);
        }

        _sessions.Remove(executionId);
    }
}
