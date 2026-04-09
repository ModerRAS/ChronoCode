using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ChronoCode.Services;

public interface IOpencodeServerManager
{
    Task StartServerAsync(CancellationToken cancellationToken = default);
    Task StopServerAsync();
    bool IsServerRunning { get; }
    string ServerUrl { get; }
    Task<bool> WaitForServerReadyAsync(TimeSpan timeout);
}

public class OpencodeServerManager : IOpencodeServerManager, IDisposable
{
    private readonly ILogger<OpencodeServerManager> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private Process? _serverProcess;
    private CancellationTokenSource? _cts;
    private bool _isRunning;

    public bool IsServerRunning => _isRunning;
    public string ServerUrl => $"http://{Host}:{Port}";

    private string Host => _configuration["Opencode:Host"] ?? "127.0.0.1";
    private int Port => int.TryParse(_configuration["Opencode:Port"], out var p) ? p : 4096;
    private string? Password => _configuration["Opencode:Password"];
    private string? Username => _configuration["Opencode:Username"];

    public OpencodeServerManager(ILogger<OpencodeServerManager> logger, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public async Task StartServerAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogInformation("Opencode server is already running at {Url}", ServerUrl);
            return;
        }

        _logger.LogInformation("Starting opencode server on {Host}:{Port}", Host, Port);

        var args = new List<string> { "serve", $"--hostname={Host}", $"--port={Port}" };

        var env = new Dictionary<string, string?>
        {
            { "PATH", Environment.GetEnvironmentVariable("PATH") }
        };

        if (!string.IsNullOrEmpty(Password))
        {
            env["OPENCODE_SERVER_PASSWORD"] = Password;
            if (!string.IsNullOrEmpty(Username))
            {
                env["OPENCODE_SERVER_USERNAME"] = Username;
            }
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _serverProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "opencode",
                    Arguments = string.Join(" ", args),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            foreach (var kvp in env)
            {
                if (kvp.Value != null)
                {
                    _serverProcess.StartInfo.Environment[kvp.Key] = kvp.Value;
                }
            }

            _serverProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogDebug("Opencode: {Output}", e.Data);
                }
            };

            _serverProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogError("Opencode Error: {Error}", e.Data);
                }
            };

            _serverProcess.Start();
            _serverProcess.BeginOutputReadLine();
            _serverProcess.BeginErrorReadLine();

            var ready = await WaitForServerReadyAsync(TimeSpan.FromSeconds(30), _cts.Token);
            if (!ready)
            {
                throw new TimeoutException("Opencode server did not start within the expected time");
            }

            _isRunning = true;
            _logger.LogInformation("Opencode server started at {Url}", ServerUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start opencode server");
            await StopServerAsync();
            throw;
        }
    }

    public async Task<bool> WaitForServerReadyAsync(TimeSpan timeout)
    {
        return await WaitForServerReadyAsync(timeout, CancellationToken.None);
    }

    private async Task<bool> WaitForServerReadyAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("OpencodeServer");
        var endTime = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < endTime)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            try
            {
                var response = await client.GetAsync($"{ServerUrl}/path", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Server not ready yet
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }

    public async Task StopServerAsync()
    {
        if (!_isRunning || _serverProcess == null)
        {
            return;
        }

        _logger.LogInformation("Stopping opencode server");

        try
        {
            _cts?.Cancel();
            _serverProcess.Kill(true);
            await _serverProcess.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping opencode server");
        }
        finally
        {
            _isRunning = false;
            _serverProcess.Dispose();
            _serverProcess = null;
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Dispose()
    {
        // Use GetAwaiter().GetResult() to avoid deadlock while still being synchronous
        StopServerAsync().GetAwaiter().GetResult();
    }
}
