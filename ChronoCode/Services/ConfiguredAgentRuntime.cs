namespace ChronoCode.Services;

public class ConfiguredAgentRuntime : IAgentRuntime
{
    private readonly IConfiguration _configuration;
    private readonly OpencodeRuntime _opencodeRuntime;
    private readonly PiRuntime _piRuntime;

    public ConfiguredAgentRuntime(
        IConfiguration configuration,
        OpencodeRuntime opencodeRuntime,
        PiRuntime piRuntime)
    {
        _configuration = configuration;
        _opencodeRuntime = opencodeRuntime;
        _piRuntime = piRuntime;
    }

    public AgentRuntimeStatus GetStatus()
    {
        return GetRuntime().GetStatus();
    }

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        return GetRuntime().EnsureReadyAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return GetRuntime().StopAsync(cancellationToken);
    }

    public Task<AgentExecutionSession> EnsureExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        Func<string, Task> onChunk,
        string? sessionRef = null,
        CancellationToken cancellationToken = default)
    {
        return GetRuntime().EnsureExecutionSessionAsync(executionId, workingDirectory, onChunk, sessionRef, cancellationToken);
    }

    public Task<string> SendMessageAsync(
        Guid executionId,
        string workingDirectory,
        string prompt,
        AgentMessageMode mode,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        return GetRuntime().SendMessageAsync(executionId, workingDirectory, prompt, mode, onChunk, cancellationToken);
    }

    public Task<AgentExecutionSession?> GetExecutionSessionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        return GetRuntime().GetExecutionSessionAsync(executionId, cancellationToken);
    }

    public Task<AgentExecutionSession> ResumeExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        string sessionRef,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        return GetRuntime().ResumeExecutionSessionAsync(executionId, workingDirectory, sessionRef, onChunk, cancellationToken);
    }

    public Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        return GetRuntime().StopExecutionAsync(executionId, cancellationToken);
    }

    private IAgentRuntime GetRuntime()
    {
        return (_configuration["AgentRuntime:Backend"] ?? "opencode")
            .Trim()
            .ToLowerInvariant() switch
        {
            "pi" => _piRuntime,
            _ => _opencodeRuntime
        };
    }
}
