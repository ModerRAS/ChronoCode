using ChronoCode.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ChronoCode.Tests;

public class SetupControllerTests
{
    [Fact]
    public async Task Get_Status_ReturnsNotInitialized_WhenNoDatabaseProviderConfigured()
    {
        await using var factory = CreateFactory(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = string.Empty,
                ["ConnectionStrings:DefaultConnection"] = string.Empty,
                ["ConnectionStrings:SqliteConnection"] = string.Empty,
            });
        });

        var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<SetupStatusDto>("/api/setup/status");

        Assert.NotNull(status);
        Assert.False(status.Initialized);
        Assert.Equal("storage/chronocode.db", status.DefaultSqlitePath);
    }

    [Fact]
    public async Task Get_Tasks_ReturnsSetupRequired_WhenApplicationNotInitialized()
    {
        await using var factory = CreateFactory(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Provider"] = string.Empty,
                ["ConnectionStrings:DefaultConnection"] = string.Empty,
                ["ConnectionStrings:SqliteConnection"] = string.Empty,
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(Action<IConfigurationBuilder> configure)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureAppConfiguration((_, config) => configure(config));
        });
    }
}
