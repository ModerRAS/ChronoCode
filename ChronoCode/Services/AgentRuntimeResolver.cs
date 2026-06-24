using ChronoCode.Models.Workflow;

namespace ChronoCode.Services;

/// <summary>
/// Resolves the concrete <see cref="IAgentRuntime"/> for a backend name. Workflow
/// agent nodes resolve via the node/task backend (pi only — enforced by the engine);
/// chat/legacy callers pass the configured or null backend.
/// </summary>
public sealed class AgentRuntimeResolver : IAgentRuntimeResolver
{
    private readonly IConfiguration _configuration;
    private readonly OpencodeRuntime _opencodeRuntime;
    private readonly PiRuntime _piRuntime;

    public AgentRuntimeResolver(IConfiguration configuration, OpencodeRuntime opencodeRuntime, PiRuntime piRuntime)
    {
        _configuration = configuration;
        _opencodeRuntime = opencodeRuntime;
        _piRuntime = piRuntime;
    }

    public IAgentRuntime Get(string? backend)
    {
        var name = (backend ?? _configuration["AgentRuntime:Backend"] ?? WorkflowBackend.Opencode)
            .Trim()
            .ToLowerInvariant();

        return name switch
        {
            WorkflowBackend.Pi => _piRuntime,
            _ => _opencodeRuntime
        };
    }

    public AgentRuntimeStatus GetStatus(string? backend) => Get(backend).GetStatus();
}
