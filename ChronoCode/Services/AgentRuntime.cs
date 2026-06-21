namespace ChronoCode.Services;

public sealed record AgentRuntimeStatus(
    string Backend,
    bool IsReady,
    string? Endpoint,
    bool SupportsLifecycleControls,
    bool SupportsPersistentSessions,
    bool SupportsSupplementalMessages
);

public sealed record AgentExecutionSession(
    string Backend,
    string? SessionId,
    string? SessionFile,
    string WorkingDirectory,
    bool SupportsSupplementalMessages
);

public enum AgentMessageMode
{
    Prompt,
    Steer,
    FollowUp
}

public interface IAgentRuntime
{
    AgentRuntimeStatus GetStatus();

    Task EnsureReadyAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<AgentExecutionSession> EnsureExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        Func<string, Task> onChunk,
        string? sessionRef = null,
        CancellationToken cancellationToken = default);

    Task<string> SendMessageAsync(
        Guid executionId,
        string workingDirectory,
        string prompt,
        AgentMessageMode mode,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default);

    Task<AgentExecutionSession?> GetExecutionSessionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);
}
