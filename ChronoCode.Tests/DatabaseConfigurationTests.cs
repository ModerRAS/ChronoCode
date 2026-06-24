using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChronoCode.Tests;

public class DatabaseConfigurationTests
{
    [Theory]
    [InlineData("pgsql", DatabaseConfiguration.PostgreSqlProvider)]
    [InlineData("postgres", DatabaseConfiguration.PostgreSqlProvider)]
    [InlineData("postgresql", DatabaseConfiguration.PostgreSqlProvider)]
    [InlineData("sqlite", DatabaseConfiguration.SqliteProvider)]
    public void NormalizeProvider_MapsAliases(string input, string expected)
    {
        Assert.Equal(expected, DatabaseConfiguration.NormalizeProvider(input));
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    [InlineData("mysql", null)]
    [InlineData("mongodb", null)]
    [InlineData("SQLITE", DatabaseConfiguration.SqliteProvider)]
    [InlineData("  PostgreSQL  ", DatabaseConfiguration.PostgreSqlProvider)]
    public void NormalizeProvider_HandlesEdgeCases(string? input, string? expected)
    {
        Assert.Equal(expected, DatabaseConfiguration.NormalizeProvider(input));
    }

    [Fact]
    public void BuildSqliteConnectionString_CreatesAbsoluteDataSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var connectionString = DatabaseConfiguration.BuildSqliteConnectionString("data/test.db", root);

        Assert.Contains("Data Source=", connectionString);
        Assert.Contains("test.db", connectionString);
    }

    [Fact]
    public void BuildSqliteConnectionString_UsesDefaultPath_WhenEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var connectionString = DatabaseConfiguration.BuildSqliteConnectionString("", root);

        // The default path is storage/chronocode.db, so the full path should contain chronocode.db
        Assert.Contains("chronocode.db", connectionString);
        Assert.Contains("Data Source=", connectionString);
    }

    [Fact]
    public void BuildSqliteConnectionString_UsesAbsolutePath_WhenRooted()
    {
        var absPath = Path.Combine(Path.GetTempPath(), "abs-test.db");
        var connectionString = DatabaseConfiguration.BuildSqliteConnectionString(absPath, "/irrelevant");

        Assert.Contains(absPath, connectionString);
    }

    [Fact]
    public void BuildSqliteConnectionString_CreatesDirectory_IfMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var nestedDir = Path.Combine(root, "a", "b", "c");

        DatabaseConfiguration.BuildSqliteConnectionString("a/b/c/test.db", root);

        Assert.True(Directory.Exists(nestedDir));
    }

    [Fact]
    public void BuildPostgreSqlConnectionString_BuildsValidNpgsqlString()
    {
        var cs = DatabaseConfiguration.BuildPostgreSqlConnectionString(
            "localhost", 5433, "mydb", "admin", "secret");

        Assert.Contains("Host=localhost", cs);
        Assert.Contains("Port=5433", cs);
        Assert.Contains("Database=mydb", cs);
        Assert.Contains("Username=admin", cs);
        Assert.Contains("Password=secret", cs);
    }

    [Fact]
    public void BuildPostgreSqlConnectionString_EmptyPassword_WhenNull()
    {
        var cs = DatabaseConfiguration.BuildPostgreSqlConnectionString(
            "localhost", 5432, "db", "user", null);

        Assert.Contains("Password=", cs);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenProviderMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var environment = new FakeHostEnvironment();

        Assert.False(DatabaseConfiguration.IsConfigured(configuration, environment));
        Assert.False(DatabaseConfiguration.CreateRuntimeState(configuration).Initialized);
    }

    [Fact]
    public void CreateRuntimeState_ReturnsInitialized_WhenSqliteConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "sqlite",
                ["ConnectionStrings:SqliteConnection"] = "Data Source=:memory:"
            })
            .Build();

        var state = DatabaseConfiguration.CreateRuntimeState(configuration);

        Assert.True(state.Initialized);
        Assert.Equal(DatabaseConfiguration.SqliteProvider, state.Provider);
        Assert.Equal("Data Source=:memory:", state.ConnectionString);
    }

    [Fact]
    public void CreateRuntimeState_ReturnsInitialized_WhenPostgreSqlConfigured()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = "postgresql",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test"
            })
            .Build();

        var state = DatabaseConfiguration.CreateRuntimeState(configuration);

        Assert.True(state.Initialized);
        Assert.Equal(DatabaseConfiguration.PostgreSqlProvider, state.Provider);
    }

    [Fact]
    public void IsConfigured_StateOverload_ReturnsStateInitialized()
    {
        var state = new DatabaseRuntimeState(DatabaseConfiguration.SqliteProvider, "Data Source=:memory:");
        Assert.True(DatabaseConfiguration.IsConfigured(state));

        var emptyState = new DatabaseRuntimeState(null, null);
        Assert.False(DatabaseConfiguration.IsConfigured(emptyState));
    }

    [Fact]
    public void Configure_ThrowsForUnsupportedProvider()
    {
        var options = new DbContextOptionsBuilder();
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseConfiguration.Configure(options, "mysql", "some-connection"));
    }

    [Fact]
    public void Configure_FallsBackToSetupDb_WhenNotInitialized()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var environment = new FakeHostEnvironment { ContentRootPath = root };
        var state = new DatabaseRuntimeState(null, null);

        var options = new DbContextOptionsBuilder();
        // Should not throw — falls back to setup-mode SQLite
        DatabaseConfiguration.Configure(options, state, environment);

        // Verify SQLite was configured (the options extension exists)
        var extension = options.Options.Extensions.FirstOrDefault();
        Assert.NotNull(extension);
    }

    [Fact]
    public void Configure_WithValidSqliteState_ConfiguresSqlite()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var environment = new FakeHostEnvironment { ContentRootPath = root };
        var state = new DatabaseRuntimeState(DatabaseConfiguration.SqliteProvider, "Data Source=:memory:");

        var options = new DbContextOptionsBuilder();
        DatabaseConfiguration.Configure(options, state, environment);

        Assert.NotNull(options.Options.Extensions.FirstOrDefault());
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ChronoCode.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }
}
