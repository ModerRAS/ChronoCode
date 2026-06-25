using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ChronoCode.Tests;

public class SettingsControllerTests
{
    [Fact]
    public async Task Get_Settings_ReturnsConfiguredRuntimeValues()
    {
        await using var host = CreateHost((config, tempRoot) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = DatabaseConfiguration.SqliteProvider,
                ["ConnectionStrings:SqliteConnection"] = BuildSqliteConnectionString(tempRoot),
                ["ConnectionStrings:DefaultConnection"] = string.Empty,
                ["AgentRuntime:Backend"] = "opencode",
                ["Opencode:Host"] = "10.0.0.20",
                ["Opencode:Port"] = "5050",
                ["Opencode:Username"] = "alice",
                ["Opencode:Password"] = "secret-value",
                ["Pi:Provider"] = "openrouter",
                ["Pi:Model"] = "claude-3.7-sonnet",
                ["Pi:Thinking"] = "high"
            });
        });

        var settings = await host.Client.GetFromJsonAsync<RuntimeSettingsDto>("/api/settings");

        Assert.NotNull(settings);
        Assert.Equal("opencode", settings.AgentRuntime.Backend);
        Assert.Equal("10.0.0.20", settings.Opencode.Host);
        Assert.Equal(5050, settings.Opencode.Port);
        Assert.Equal("alice", settings.Opencode.Username);
        Assert.True(settings.Opencode.HasPassword);
        Assert.Equal("openrouter", settings.Pi.Provider);
        Assert.Equal("claude-3.7-sonnet", settings.Pi.Model);
        Assert.Equal("high", settings.Pi.Thinking);
    }

    [Fact]
    public async Task Put_Settings_WritesRuntimeSections_AndPreservesDatabaseSections()
    {
        const string defaultConnection = "Host=db;Database=chronocode;Username=tester;Password=secret";

        await using var host = CreateHost(
            localConfigFactory: tempRoot => new JsonObject
            {
                ["Database"] = new JsonObject
                {
                    ["Provider"] = DatabaseConfiguration.SqliteProvider
                },
                ["ConnectionStrings"] = new JsonObject
                {
                    ["SqliteConnection"] = BuildSqliteConnectionString(tempRoot),
                    ["DefaultConnection"] = defaultConnection
                }
            });

        var response = await host.Client.PutAsJsonAsync("/api/settings", new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto
            {
                Backend = "opencode"
            },
            Opencode = new UpdateOpencodeSettingsDto
            {
                Host = " 192.168.1.8 ",
                Port = 1234,
                Username = " operator ",
                Password = "  topsecret  "
            },
            Pi = new UpdatePiSettingsDto
            {
                Provider = " openrouter ",
                Model = " llama-3.3 ",
                Thinking = " low "
            }
        });

        response.EnsureSuccessStatusCode();

        var file = JsonNode.Parse(await File.ReadAllTextAsync(host.ConfigPath))!.AsObject();
        Assert.Equal(DatabaseConfiguration.SqliteProvider, file["Database"]?["Provider"]?.GetValue<string>());
        Assert.Equal(BuildSqliteConnectionString(host.TempRoot), file["ConnectionStrings"]?["SqliteConnection"]?.GetValue<string>());
        Assert.Equal(defaultConnection, file["ConnectionStrings"]?["DefaultConnection"]?.GetValue<string>());
        Assert.Equal("opencode", file["AgentRuntime"]?["Backend"]?.GetValue<string>());
        Assert.Equal("192.168.1.8", file["Opencode"]?["Host"]?.GetValue<string>());
        Assert.Equal(1234, file["Opencode"]?["Port"]?.GetValue<int>());
        Assert.Equal("operator", file["Opencode"]?["Username"]?.GetValue<string>());
        Assert.Equal("topsecret", file["Opencode"]?["Password"]?.GetValue<string>());
        Assert.Equal("openrouter", file["Pi"]?["Provider"]?.GetValue<string>());
        Assert.Equal("llama-3.3", file["Pi"]?["Model"]?.GetValue<string>());
        Assert.Equal("low", file["Pi"]?["Thinking"]?.GetValue<string>());
    }

    [Fact]
    public async Task Put_Settings_PreservesExistingPassword_WhenPasswordOmitted()
    {
        await using var host = CreateHost(
            localConfigFactory: tempRoot => new JsonObject
            {
                ["Database"] = new JsonObject
                {
                    ["Provider"] = DatabaseConfiguration.SqliteProvider
                },
                ["ConnectionStrings"] = new JsonObject
                {
                    ["SqliteConnection"] = BuildSqliteConnectionString(tempRoot),
                    ["DefaultConnection"] = string.Empty
                },
                ["Opencode"] = new JsonObject
                {
                    ["Host"] = "127.0.0.1",
                    ["Port"] = 4096,
                    ["Username"] = "saved-user",
                    ["Password"] = "saved-password"
                }
            });

        const string json = """
        {
          "agentRuntime": {
            "backend": "opencode"
          },
          "opencode": {
            "host": "localhost",
            "port": 4096,
            "username": "saved-user"
          },
          "pi": {
            "provider": "",
            "model": "",
            "thinking": "medium"
          }
        }
        """;

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var putResponse = await host.Client.PutAsync("/api/settings", content);
        putResponse.EnsureSuccessStatusCode();

        var settings = await host.Client.GetFromJsonAsync<RuntimeSettingsDto>("/api/settings");
        Assert.NotNull(settings);
        Assert.True(settings.Opencode.HasPassword);

        var file = JsonNode.Parse(await File.ReadAllTextAsync(host.ConfigPath))!.AsObject();
        Assert.Equal("saved-password", file["Opencode"]?["Password"]?.GetValue<string>());
    }

    private static TestHost CreateHost(
        Action<IConfigurationBuilder, string>? configure = null,
        Func<string, JsonObject>? localConfigFactory = null)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(Path.Combine(tempRoot, "storage"));
        Directory.CreateDirectory(Path.Combine(tempRoot, "wwwroot"));
        File.WriteAllText(Path.Combine(tempRoot, "wwwroot", "index.html"), "<html><body>ChronoCode</body></html>");

        var configPath = Path.Combine(tempRoot, DatabaseConfiguration.LocalConfigFileName);
        if (localConfigFactory != null)
        {
            var localConfig = localConfigFactory(tempRoot);
            File.WriteAllText(
                configPath,
                localConfig.ToJsonString(new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(tempRoot);
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => configure?.Invoke(config, tempRoot));
        });

        return new TestHost(factory, factory.CreateClient(), tempRoot, configPath);
    }

    private static string BuildSqliteConnectionString(string tempRoot)
    {
        return $"Data Source={Path.Combine(tempRoot, "storage", "chronocode-tests.db")}";
    }

    private sealed class TestHost : IAsyncDisposable
    {
        public WebApplicationFactory<Program> Factory { get; }
        public HttpClient Client { get; }
        public string TempRoot { get; }
        public string ConfigPath { get; }

        public TestHost(WebApplicationFactory<Program> factory, HttpClient client, string tempRoot, string configPath)
        {
            Factory = factory;
            Client = client;
            TempRoot = tempRoot;
            ConfigPath = configPath;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Factory.DisposeAsync();
            // Release pooled SQLite file handles so the temp directory can be deleted.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(TempRoot))
            {
                Directory.Delete(TempRoot, recursive: true);
            }
        }
    }
}
