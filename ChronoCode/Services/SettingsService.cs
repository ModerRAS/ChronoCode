using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Models.DTOs;

namespace ChronoCode.Services;

public interface ISettingsService
{
    Task<RuntimeSettingsDto> GetAsync();
    Task<RuntimeSettingsDto> UpdateAsync(UpdateRuntimeSettingsDto request, CancellationToken cancellationToken);
}

public class SettingsService : ISettingsService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public SettingsService(IConfiguration configuration, IHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public Task<RuntimeSettingsDto> GetAsync()
    {
        return Task.FromResult(new RuntimeSettingsDto
        {
            AgentRuntime = new AgentRuntimeSettingsDto
            {
                Backend = ReadString("AgentRuntime:Backend", "pi")
            },
            Opencode = new OpencodeSettingsDto
            {
                Host = ReadString("Opencode:Host", "127.0.0.1"),
                Port = ReadInt("Opencode:Port", 4096),
                Username = ReadString("Opencode:Username", string.Empty),
                HasPassword = !string.IsNullOrWhiteSpace(_configuration["Opencode:Password"])
            },
            Pi = new PiSettingsDto
            {
                Provider = ReadString("Pi:Provider", string.Empty),
                Model = ReadString("Pi:Model", string.Empty),
                Thinking = ReadString("Pi:Thinking", "medium")
            }
        });
    }

    public async Task<RuntimeSettingsDto> UpdateAsync(UpdateRuntimeSettingsDto request, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(_environment.ContentRootPath, DatabaseConfiguration.LocalConfigFileName);
        JsonObject root;

        if (File.Exists(configPath))
        {
            root = JsonNode.Parse(await File.ReadAllTextAsync(configPath, cancellationToken)) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        var existingOpencode = root["Opencode"] as JsonObject;
        var existingPassword = existingOpencode?["Password"]?.GetValue<string>() ?? _configuration["Opencode:Password"];

        root["AgentRuntime"] = new JsonObject
        {
            ["Backend"] = request.AgentRuntime.Backend.Trim()
        };

        var opencode = new JsonObject
        {
            ["Host"] = request.Opencode.Host.Trim(),
            ["Port"] = request.Opencode.Port,
            ["Username"] = request.Opencode.Username.Trim()
        };

        if (!string.IsNullOrWhiteSpace(request.Opencode.Password))
        {
            opencode["Password"] = request.Opencode.Password.Trim();
        }
        else if (existingPassword is not null)
        {
            opencode["Password"] = existingPassword;
        }

        root["Opencode"] = opencode;

        root["Pi"] = new JsonObject
        {
            ["Provider"] = request.Pi.Provider.Trim(),
            ["Model"] = request.Pi.Model.Trim(),
            ["Thinking"] = request.Pi.Thinking.Trim()
        };

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(configPath, json, cancellationToken);
        (_configuration as IConfigurationRoot)?.Reload();
        return await GetAsync();
    }

    private string ReadString(string key, string defaultValue)
    {
        var value = _configuration[key];
        return value == null ? defaultValue : value.Trim();
    }

    private int ReadInt(string key, int defaultValue)
    {
        return int.TryParse(_configuration[key], out var value) ? value : defaultValue;
    }
}
