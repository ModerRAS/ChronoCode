// Intentionally empty. The former ConfiguredAgentRuntime adapter was replaced by
// IAgentRuntimeResolver (AgentRuntimeResolver). ChatRuntimeService and the workflow
// engine resolve runtimes through the resolver. This file is retained as an
// extension point; the Controllers subagent removes the DI registration in Program.cs.
namespace ChronoCode.Services;
