using System.Diagnostics;
using System.Reflection;
using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

public class OpencodeServerManagerTests
{
    [Fact]
    public async Task StopServerAsync_StopsProcess_EvenWhenRunningFlagIsFalse()
    {
        var process = StartLongRunningProcess();
        var processId = process.Id;

        try
        {
            var manager = CreateManager();
            SetPrivateField(manager, "_serverProcess", process);
            SetPrivateField(manager, "_cts", new CancellationTokenSource());
            SetPrivateField(manager, "_isRunning", false);

            await manager.StopServerAsync();

            Assert.False(manager.IsServerRunning);
            AssertProcessExited(processId);
        }
        finally
        {
            if (TryGetProcess(processId, out var runningProcess))
            {
                Assert.NotNull(runningProcess);
                runningProcess.Kill(entireProcessTree: true);
                await runningProcess.WaitForExitAsync();
                runningProcess.Dispose();
            }

            process.Dispose();
        }
    }

    [Fact]
    public async Task IsServerRunning_ReturnsFalse_WhenTrackedProcessHasExited()
    {
        var process = StartImmediateExitProcess();
        await process.WaitForExitAsync();

        try
        {
            var manager = CreateManager();
            SetPrivateField(manager, "_serverProcess", process);
            SetPrivateField(manager, "_isRunning", true);

            Assert.False(manager.IsServerRunning);
        }
        finally
        {
            process.Dispose();
        }
    }

    private static OpencodeServerManager CreateManager()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            configuration,
            new StubHttpClientFactory());
    }

    private static Process StartLongRunningProcess()
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", "-NoProfile -Command Start-Sleep -Seconds 30")
            : new ProcessStartInfo("/bin/sh", "-c \"sleep 30\"");

        startInfo.UseShellExecute = false;
        return Process.Start(startInfo)!;
    }

    private static Process StartImmediateExitProcess()
    {
        var startInfo = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", "-NoProfile -Command exit 0")
            : new ProcessStartInfo("/bin/sh", "-c \"true\"");

        startInfo.UseShellExecute = false;
        return Process.Start(startInfo)!;
    }

    private static void SetPrivateField(object target, string fieldName, object? value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }

    private static void AssertProcessExited(int processId)
    {
        Assert.False(TryGetProcess(processId, out var runningProcess));
        runningProcess?.Dispose();
    }

    private static bool TryGetProcess(int processId, out Process? process)
    {
        try
        {
            process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            process = null;
            return false;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
