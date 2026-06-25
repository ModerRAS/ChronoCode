using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Tests for DatabaseRuntimeState, SettingsService, and SetupService.
/// </summary>
public class MiscServiceTests : IDisposable
{
    private readonly string _tempDir;

    public MiscServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { /* SQLite files may still be locked */ }
    }

    // ---- DatabaseRuntimeState ----

    [Fact]
    public void DatabaseRuntimeState_NotInitialized_WhenProviderNull()
    {
        var state = new DatabaseRuntimeState(null, "Data Source=:memory:");
        Assert.False(state.Initialized);
        Assert.Null(state.Provider);
    }

    [Fact]
    public void DatabaseRuntimeState_NotInitialized_WhenConnectionStringNull()
    {
        var state = new DatabaseRuntimeState("sqlite", null);
        Assert.False(state.Initialized);
    }

    [Fact]
    public void DatabaseRuntimeState_NotInitialized_WhenConnectionStringEmpty()
    {
        var state = new DatabaseRuntimeState("sqlite", "   ");
        Assert.False(state.Initialized);
        Assert.Null(state.ConnectionString);
    }

    [Fact]
    public void DatabaseRuntimeState_Initialized_WhenBothSet()
    {
        var state = new DatabaseRuntimeState("sqlite", "Data Source=:memory:");
        Assert.True(state.Initialized);
        Assert.Equal("sqlite", state.Provider);
        Assert.Equal("Data Source=:memory:", state.ConnectionString);
    }

    [Fact]
    public void DatabaseRuntimeState_NormalizesProviderInConstructor()
    {
        var state = new DatabaseRuntimeState("PostgreSQL", "Host=localhost");
        Assert.Equal(DatabaseConfiguration.PostgreSqlProvider, state.Provider);
    }

    [Fact]
    public void DatabaseRuntimeState_SetConfigured_UpdatesState()
    {
        var state = new DatabaseRuntimeState(null, null);
        Assert.False(state.Initialized);

        state.SetConfigured("sqlite", "Data Source=test.db");

        Assert.True(state.Initialized);
        Assert.Equal("sqlite", state.Provider);
    }

    [Fact]
    public void DatabaseRuntimeState_SetConfigured_ThrowsForUnsupportedProvider()
    {
        var state = new DatabaseRuntimeState(null, null);
        Assert.Throws<InvalidOperationException>(() => state.SetConfigured("mysql", "some-conn"));
    }

    // ---- SettingsService ----

    [Fact]
    public async Task SettingsService_Get_ReturnsDefaults_WhenConfigEmpty()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var service = new SettingsService(config, env);

        var result = await service.GetAsync();

        Assert.Equal("pi", result.AgentRuntime.Backend);
        Assert.Equal("127.0.0.1", result.Opencode.Host);
        Assert.Equal(4096, result.Opencode.Port);
        Assert.False(result.Opencode.HasPassword);
        Assert.Equal("medium", result.Pi.Thinking);
    }

    [Fact]
    public async Task SettingsService_Get_ReturnsConfiguredValues()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentRuntime:Backend"] = "pi",
                ["Opencode:Host"] = "0.0.0.0",
                ["Opencode:Port"] = "8443",
                ["Opencode:Password"] = "secret",
                ["Pi:Provider"] = "anthropic",
                ["Pi:Model"] = "claude-sonnet-4-20250514",
                ["Pi:Thinking"] = "high"
            })
            .Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var service = new SettingsService(config, env);

        var result = await service.GetAsync();

        Assert.Equal("pi", result.AgentRuntime.Backend);
        Assert.Equal("0.0.0.0", result.Opencode.Host);
        Assert.Equal(8443, result.Opencode.Port);
        Assert.True(result.Opencode.HasPassword);
        Assert.Equal("anthropic", result.Pi.Provider);
        Assert.Equal("claude-sonnet-4-20250514", result.Pi.Model);
        Assert.Equal("high", result.Pi.Thinking);
    }

    [Fact]
    public async Task SettingsService_Update_WritesLocalConfig()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var service = new SettingsService(config, env);

        await service.UpdateAsync(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Host = "localhost", Port = 4096, Username = "admin", Password = "pw123" },
            Pi = new UpdatePiSettingsDto { Provider = "anthropic", Model = "claude", Thinking = "low" }
        }, CancellationToken.None);

        var configPath = Path.Combine(_tempDir, DatabaseConfiguration.LocalConfigFileName);
        Assert.True(File.Exists(configPath));
        var json = await File.ReadAllTextAsync(configPath);
        Assert.Contains("pi", json);
        Assert.Contains("pw123", json);
        Assert.Contains("anthropic", json);
    }

    [Fact]
    public async Task SettingsService_Update_PreservesExistingPassword_WhenEmpty()
    {
        // First write with a password
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var service = new SettingsService(config, env);

        await service.UpdateAsync(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Host = "localhost", Port = 4096, Username = "admin", Password = "original-pw" },
            Pi = new UpdatePiSettingsDto { Provider = "", Model = "", Thinking = "medium" }
        }, CancellationToken.None);

        // Now update without a password
        await service.UpdateAsync(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Host = "localhost", Port = 4096, Username = "admin", Password = "" },
            Pi = new UpdatePiSettingsDto { Provider = "", Model = "", Thinking = "medium" }
        }, CancellationToken.None);

        var configPath = Path.Combine(_tempDir, DatabaseConfiguration.LocalConfigFileName);
        var json = await File.ReadAllTextAsync(configPath);
        Assert.Contains("original-pw", json);
    }

    [Fact]
    public async Task SettingsService_Update_ReturnsUpdatedSettings()
    {
        var configPath = Path.Combine(_tempDir, DatabaseConfiguration.LocalConfigFileName);
        var config = new ConfigurationBuilder()
            .SetBasePath(_tempDir)
            .AddJsonFile(DatabaseConfiguration.LocalConfigFileName, optional: true, reloadOnChange: true)
            .Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var service = new SettingsService(config, env);

        var result = await service.UpdateAsync(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Host = "0.0.0.0", Port = 8443, Username = "admin", Password = "pw" },
            Pi = new UpdatePiSettingsDto { Provider = "anthropic", Model = "claude", Thinking = "high" }
        }, CancellationToken.None);

        Assert.Equal("0.0.0.0", result.Opencode.Host);
        Assert.Equal(8443, result.Opencode.Port);
        Assert.True(result.Opencode.HasPassword);
        Assert.Equal("anthropic", result.Pi.Provider);
    }

    // ---- SetupService ----

    [Fact]
    public void SetupService_GetStatus_ReturnsNotInitialized_WhenStateEmpty()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState(null, null);
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        var status = service.GetStatus();

        Assert.False(status.Initialized);
        Assert.Null(status.DatabaseProvider);
        Assert.Equal(DatabaseConfiguration.DefaultSqlitePath, status.DefaultSqlitePath);
    }

    [Fact]
    public void SetupService_GetStatus_ReturnsInitialized_WhenStateConfigured()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState("sqlite", "Data Source=:memory:");
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        var status = service.GetStatus();

        Assert.True(status.Initialized);
        Assert.Equal("sqlite", status.DatabaseProvider);
    }

    [Fact]
    public async Task SetupService_Initialize_ThrowsWhenAlreadyInitialized()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState("sqlite", "Data Source=:memory:");
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.InitializeAsync(new InitializeSetupDto { DatabaseProvider = "sqlite" }));
    }

    [Fact]
    public async Task SetupService_Initialize_Sqlite_WritesConfigAndUpdatesState()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "storage"));
        var config = new ConfigurationBuilder()
            .SetBasePath(_tempDir)
            .AddJsonFile(DatabaseConfiguration.LocalConfigFileName, optional: true, reloadOnChange: true)
            .Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState(null, null);
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        var result = await service.InitializeAsync(new InitializeSetupDto
        {
            DatabaseProvider = "sqlite",
            SqlitePath = "storage/test.db"
        });

        Assert.True(result.Initialized);
        Assert.Equal("sqlite", result.DatabaseProvider);
        Assert.True(state.Initialized);

        var configPath = Path.Combine(_tempDir, DatabaseConfiguration.LocalConfigFileName);
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public async Task SetupService_Initialize_PostgreSql_WithConnectionString()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState(null, null);
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        // Use a connection string that won't actually connect, but the config writing should work
        // InitializeAsync will try to migrate which will fail, but we test the validation before that
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.InitializeAsync(new InitializeSetupDto
            {
                DatabaseProvider = "postgresql",
                ConnectionString = "Host=nonexistent;Database=test;Username=test;Password=test;Timeout=1"
            }));
    }

    [Fact]
    public async Task SetupService_Initialize_PostgreSql_MissingRequiredFields_Throws()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState(null, null);
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InitializeAsync(new InitializeSetupDto
            {
                DatabaseProvider = "postgresql"
                // Missing host, database, username
            }));
    }

    [Fact]
    public async Task SetupService_Initialize_UnsupportedProvider_Throws()
    {
        var config = new ConfigurationBuilder().Build();
        var env = new FakeHostEnvironment { ContentRootPath = _tempDir };
        var state = new DatabaseRuntimeState(null, null);
        var service = new SetupService(config, env, state, NullLogger<SetupService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.InitializeAsync(new InitializeSetupDto
            {
                DatabaseProvider = "mysql"
            }));
    }

    // ---- Fake ----

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ChronoCode.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
