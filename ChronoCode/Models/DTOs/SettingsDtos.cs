namespace ChronoCode.Models.DTOs;

public sealed class RuntimeSettingsDto
{
    public AgentRuntimeSettingsDto AgentRuntime { get; set; } = new();
    public OpencodeSettingsDto Opencode { get; set; } = new();
    public PiSettingsDto Pi { get; set; } = new();
}

public sealed class UpdateRuntimeSettingsDto
{
    public UpdateAgentRuntimeSettingsDto AgentRuntime { get; set; } = new();
    public UpdateOpencodeSettingsDto Opencode { get; set; } = new();
    public UpdatePiSettingsDto Pi { get; set; } = new();
}

public sealed class AgentRuntimeSettingsDto
{
    public string Backend { get; set; } = "pi";
}

public sealed class UpdateAgentRuntimeSettingsDto
{
    public string Backend { get; set; } = "pi";
}

public sealed class OpencodeSettingsDto
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4096;
    public string Username { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
}

public sealed class UpdateOpencodeSettingsDto
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 4096;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
}

public sealed class PiSettingsDto
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Thinking { get; set; } = "medium";
}

public sealed class UpdatePiSettingsDto
{
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Thinking { get; set; } = "medium";
}
