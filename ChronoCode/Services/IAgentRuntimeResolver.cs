namespace ChronoCode.Services;

/// <summary>
/// Resolves the concrete <see cref="IAgentRuntime"/> for a backend name.
/// Workflow agent nodes resolve via task.RuntimeBackend / agent node backend;
/// only "pi" is permitted for workflow agent nodes.
/// </summary>
public interface IAgentRuntimeResolver
{
    IAgentRuntime Get(string? backend);

    AgentRuntimeStatus GetStatus(string? backend);
}
