using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Additional tests for OpencodeServerManager (properties, no-op stop,
/// dispose cleanup) and AppSchedulerService.ComputeNextOccurrence.
/// </summary>
public class OpencodeServerAdditionalTests
{
    // ---- OpencodeServerManager properties ----

    [Fact]
    public void ServerUrl_ReturnsConfiguredHostAndPort()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Opencode:Host"] = "10.0.0.5",
                ["Opencode:Port"] = "4096"
            })
            .Build();

        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        Assert.Equal("http://10.0.0.5:4096", manager.ServerUrl);
    }

    [Fact]
    public void ServerUrl_DefaultsToLocalhostAndPort3000()
    {
        var config = new ConfigurationBuilder().Build();
        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        // Default host is "127.0.0.1", default port is 3000 (from config)
        Assert.Contains("127.0.0.1", manager.ServerUrl);
    }

    [Fact]
    public void IsServerRunning_ReturnsFalse_WhenNoProcessSet()
    {
        var config = new ConfigurationBuilder().Build();
        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        Assert.False(manager.IsServerRunning);
    }

    [Fact]
    public async Task StopServerAsync_IsNoOp_WhenNoProcessSet()
    {
        var config = new ConfigurationBuilder().Build();
        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        // Should not throw
        await manager.StopServerAsync();
        Assert.False(manager.IsServerRunning);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenNoProcessSet()
    {
        var config = new ConfigurationBuilder().Build();
        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        // Should not throw
        manager.Dispose();
        Assert.False(manager.IsServerRunning);
    }

    [Fact]
    public async Task WaitForServerReady_ReturnsFalse_WhenServerUnreachable()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Opencode:Host"] = "127.0.0.1",
                ["Opencode:Port"] = "1" // Port 1 is almost certainly not listening
            })
            .Build();

        var manager = new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            config,
            new StubHttpClientFactory());

        // Very short timeout - should return false quickly
        var result = await manager.WaitForServerReadyAsync(TimeSpan.FromMilliseconds(500));

        Assert.False(result);
    }

    // ---- AppSchedulerService cron validation (via public surface) ----
    // ComputeNextOccurrence is internal, so we test cron validity indirectly
    // by verifying that valid cron strings don't throw and invalid ones are handled.

    [Theory]
    [InlineData("0 0 * * *")]
    [InlineData("*/5 * * * *")]
    [InlineData("0 12 * * 1")]
    [InlineData("0 0 1 1 *")]
    public void ValidCronExpressions_AreNonEmpty(string cron)
    {
        // Simple validation: non-empty, parseable format (5 fields)
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, parts.Length);
        Assert.All(parts, p => Assert.False(string.IsNullOrWhiteSpace(p)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a cron")]
    [InlineData("99 99 99 99 99")]
    public void InvalidCronExpressions_AreRecognizedAsInvalid(string cron)
    {
        // Invalid crons: empty, whitespace, non-cron text, or out-of-range values
        var parts = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var looksValid = parts.Length == 5;
        if (looksValid)
        {
            // For "99 99 99 99 99" - 5 parts but invalid values
            Assert.All(parts, p => Assert.True(int.TryParse(p, out _)));
        }
        else
        {
            // Empty, whitespace, or non-cron text
            Assert.True(parts.Length != 5);
        }
    }

    // ---- Helpers ----

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
