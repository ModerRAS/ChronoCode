using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
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

    [Fact]
    public void BuildSqliteConnectionString_CreatesAbsoluteDataSource()
    {
        var root = Path.Combine(Path.GetTempPath(), "chronocode-tests", Guid.NewGuid().ToString("N"));
        var connectionString = DatabaseConfiguration.BuildSqliteConnectionString("data/test.db", root);

        Assert.Contains("Data Source=", connectionString);
        Assert.Contains("test.db", connectionString);
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenProviderMissing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var environment = new FakeHostEnvironment();

        Assert.False(DatabaseConfiguration.IsConfigured(configuration, environment));
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "ChronoCode.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(Directory.GetCurrentDirectory());
    }
}
