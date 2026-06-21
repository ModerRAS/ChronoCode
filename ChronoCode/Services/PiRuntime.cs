using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ChronoCode.Services;

public class PiRuntime : IAgentRuntime
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PiRuntime> _logger;
    private readonly ConcurrentDictionary<Guid, PiExecutionState> _executions = new();
    private bool _isAvailable;

    private string Command => _configuration["Pi:Command"] ?? "pi";
    private string? Provider => EmptyToNull(_configuration["Pi:Provider"]);
    private string? Model => EmptyToNull(_configuration["Pi:Model"]);
    private string? Thinking => EmptyToNull(_configuration["Pi:Thinking"]);
    private bool ApproveProjectTrust => bool.TryParse(_configuration["Pi:ApproveProjectTrust"], out var value) ? value : true;
    private string? SessionDir => EmptyToNull(_configuration["Pi:SessionDir"]);
    private string? SessionNamePrefix => EmptyToNull(_configuration["Pi:SessionNamePrefix"]);

    public PiRuntime(IConfiguration configuration, ILogger<PiRuntime> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public AgentRuntimeStatus GetStatus()
    {
        return new AgentRuntimeStatus(
            Backend: "pi",
            IsReady: _isAvailable,
            Endpoint: null,
            SupportsLifecycleControls: true,
            SupportsPersistentSessions: true,
            SupportsSupplementalMessages: true);
    }

    public async Task EnsureReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailable)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = Command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--version");

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to start pi runtime using '{Command}': {error}");
        }

        _isAvailable = true;
        _logger.LogInformation("Pi runtime available: {Version}", string.IsNullOrWhiteSpace(output) ? "unknown" : output);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var executionId in _executions.Keys.ToList())
        {
            await StopExecutionAsync(executionId, cancellationToken);
        }
    }

    public async Task<AgentExecutionSession> EnsureExecutionSessionAsync(
        Guid executionId,
        string workingDirectory,
        Func<string, Task> onChunk,
        string? sessionRef = null,
        CancellationToken cancellationToken = default)
    {
        if (_executions.TryGetValue(executionId, out var existing))
        {
            return await existing.SessionReady.Task.WaitAsync(cancellationToken);
        }

        await EnsureReadyAsync(cancellationToken);

        var state = await StartProcessAsync(executionId, workingDirectory, onChunk, sessionRef, cancellationToken);
        _executions[executionId] = state;
        return await state.SessionReady.Task.WaitAsync(cancellationToken);
    }

    public async Task<string> SendMessageAsync(
        Guid executionId,
        string workingDirectory,
        string prompt,
        AgentMessageMode mode,
        Func<string, Task> onChunk,
        CancellationToken cancellationToken = default)
    {
        var state = _executions.TryGetValue(executionId, out var existing)
            ? existing
            : await StartProcessAsync(executionId, workingDirectory, onChunk, null, cancellationToken);

        _executions[executionId] = state;
        await state.SessionReady.Task.WaitAsync(cancellationToken);

        return mode switch
        {
            AgentMessageMode.Prompt => await SendPromptAsync(executionId, state, prompt, cancellationToken),
            AgentMessageMode.Steer => await QueueMessageAsync(executionId, state, prompt, "steer", cancellationToken),
            AgentMessageMode.FollowUp => await QueueMessageAsync(executionId, state, prompt, "follow_up", cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };
    }

    public async Task<AgentExecutionSession?> GetExecutionSessionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        if (!_executions.TryGetValue(executionId, out var state))
        {
            return null;
        }

        return await state.SessionReady.Task.WaitAsync(cancellationToken);
    }

    public async Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        if (!_executions.TryRemove(executionId, out var state))
        {
            return;
        }

        try
        {
            if (!state.Process.HasExited)
            {
                await QueueCommandAsync(state, CreateCommand($"abort-{executionId:N}", "abort"), cancellationToken);
            }
        }
        catch
        {
        }

        TryKill(state.Process);
        state.Process.Dispose();
    }

    private async Task<PiExecutionState> StartProcessAsync(
        Guid executionId,
        string workingDirectory,
        Func<string, Task> onChunk,
        string? sessionRef,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--mode");
        startInfo.ArgumentList.Add("rpc");

        if (ApproveProjectTrust)
        {
            startInfo.ArgumentList.Add("--approve");
        }

        if (!string.IsNullOrWhiteSpace(SessionDir))
        {
            startInfo.ArgumentList.Add("--session-dir");
            startInfo.ArgumentList.Add(SessionDir);
        }

        if (!string.IsNullOrWhiteSpace(Provider))
        {
            startInfo.ArgumentList.Add("--provider");
            startInfo.ArgumentList.Add(Provider);
        }

        if (!string.IsNullOrWhiteSpace(Model))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(Model);
        }

        if (!string.IsNullOrWhiteSpace(Thinking))
        {
            startInfo.ArgumentList.Add("--thinking");
            startInfo.ArgumentList.Add(Thinking);
        }

        var sessionName = BuildSessionName(executionId);
        if (!string.IsNullOrWhiteSpace(sessionName))
        {
            startInfo.ArgumentList.Add("--name");
            startInfo.ArgumentList.Add(sessionName);
        }

        if (!string.IsNullOrWhiteSpace(sessionRef))
        {
            startInfo.ArgumentList.Add("--session");
            startInfo.ArgumentList.Add(sessionRef);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Start();

        var state = new PiExecutionState(process, workingDirectory, onChunk, _logger);
        state.StdoutPump = Task.Run(() => PumpStdoutAsync(state), CancellationToken.None);
        state.StderrPump = Task.Run(() => PumpStderrAsync(state), CancellationToken.None);

        var response = await QueueCommandAsync(state, CreateCommand($"state-{executionId:N}", "get_state"), cancellationToken);
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? "Failed to read pi session state.");
        }

        return state;
    }

    private async Task<string> SendPromptAsync(
        Guid executionId,
        PiExecutionState state,
        string prompt,
        CancellationToken cancellationToken)
    {
        lock (state.SyncRoot)
        {
            if (state.ActivePromptResult != null && !state.ActivePromptResult.Task.IsCompleted)
            {
                throw new InvalidOperationException($"Execution {executionId} already has a running prompt.");
            }

            state.ActivePromptText.Clear();
            state.ActivePromptResult = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        var response = await QueueCommandAsync(
            state,
            CreateCommand($"prompt-{executionId:N}", "prompt", prompt),
            cancellationToken);

        if (!response.Success)
        {
            CompletePromptWithError(state, response.Error ?? "Prompt rejected by pi.");
            throw new InvalidOperationException(response.Error ?? "Prompt rejected by pi.");
        }

        return await state.ActivePromptResult!.Task.WaitAsync(cancellationToken);
    }

    private async Task<string> QueueMessageAsync(
        Guid executionId,
        PiExecutionState state,
        string prompt,
        string commandType,
        CancellationToken cancellationToken)
    {
        var response = await QueueCommandAsync(
            state,
            CreateCommand($"{commandType}-{executionId:N}", commandType, prompt),
            cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException(response.Error ?? $"{commandType} rejected by pi.");
        }

        return $"{commandType} queued";
    }

    private async Task<RpcCommandResponse> QueueCommandAsync(
        PiExecutionState state,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        var id = payload["id"]?.ToString() ?? Guid.NewGuid().ToString("N");
        payload["id"] = id;

        var tcs = new TaskCompletionSource<RpcCommandResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!state.PendingCommands.TryAdd(id, tcs))
        {
            throw new InvalidOperationException($"Duplicate pi rpc command id: {id}");
        }

        try
        {
            await WriteCommandAsync(state, payload, cancellationToken);
            return await tcs.Task.WaitAsync(cancellationToken);
        }
        catch
        {
            state.PendingCommands.TryRemove(id, out _);
            throw;
        }
    }

    private static Dictionary<string, object?> CreateCommand(string idPrefix, string type, string? message = null)
    {
        return new Dictionary<string, object?>
        {
            ["id"] = $"{idPrefix}-{Guid.NewGuid():N}",
            ["type"] = type,
            ["message"] = message
        };
    }

    private static async Task WriteCommandAsync(
        PiExecutionState state,
        Dictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        if (payload["message"] == null)
        {
            payload.Remove("message");
        }

        var json = JsonSerializer.Serialize(payload);
        await state.WriteLock.WaitAsync(cancellationToken);
        try
        {
            await state.Process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
            await state.Process.StandardInput.FlushAsync();
        }
        finally
        {
            state.WriteLock.Release();
        }
    }

    private async Task PumpStdoutAsync(PiExecutionState state)
    {
        try
        {
            while (true)
            {
                var line = await state.Process.StandardOutput.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var typeProperty))
                {
                    continue;
                }

                switch (typeProperty.GetString())
                {
                    case "response":
                        await HandleResponseAsync(state, root);
                        break;
                    case "message_update":
                        await HandleMessageUpdateAsync(state, root);
                        break;
                    case "message_end":
                        CompletePrompt(state);
                        break;
                    case "tool_execution_start":
                        if (root.TryGetProperty("toolName", out var toolNameStart))
                        {
                            await state.EmitAsync($"\n[tool:start] {toolNameStart.GetString()}\n");
                        }
                        break;
                    case "tool_execution_end":
                        if (root.TryGetProperty("toolName", out var toolNameEnd))
                        {
                            var suffix = root.TryGetProperty("isError", out var isError) && isError.GetBoolean()
                                ? "error"
                                : "end";
                            await state.EmitAsync($"\n[tool:{suffix}] {toolNameEnd.GetString()}\n");
                        }
                        break;
                    case "extension_error":
                        await state.EmitAsync("\n[extension_error]\n");
                        break;
                }
            }

            if (!state.SessionReady.Task.IsCompleted)
            {
                state.SessionReady.TrySetException(new InvalidOperationException("pi rpc process exited before session state was available."));
            }

            CompletePromptWithError(state, "pi rpc process exited unexpectedly.");
            FailPendingCommands(state, "pi rpc process exited unexpectedly.");
        }
        catch (Exception ex)
        {
            if (!state.SessionReady.Task.IsCompleted)
            {
                state.SessionReady.TrySetException(ex);
            }

            CompletePromptWithError(state, ex.Message);
            FailPendingCommands(state, ex.Message);
            await state.EmitAsync($"\n[pi-error] {ex.Message}\n");
        }
    }

    private async Task PumpStderrAsync(PiExecutionState state)
    {
        try
        {
            while (true)
            {
                var line = await state.Process.StandardError.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                await state.EmitAsync($"\n[pi] {line}\n");
            }
        }
        catch (Exception ex)
        {
            await state.EmitAsync($"\n[pi-error] {ex.Message}\n");
        }
    }

    private async Task HandleResponseAsync(PiExecutionState state, JsonElement root)
    {
        var id = root.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
        var command = root.TryGetProperty("command", out var commandProperty) ? commandProperty.GetString() : null;
        var success = !root.TryGetProperty("success", out var successProperty) || successProperty.GetBoolean();
        var error = root.TryGetProperty("error", out var errorProperty) ? errorProperty.GetString() : null;
        var dataJson = root.TryGetProperty("data", out var dataProperty) ? dataProperty.GetRawText() : null;

        if (command == "get_state" && success && dataJson != null)
        {
            using var dataDocument = JsonDocument.Parse(dataJson);
            var data = dataDocument.RootElement;
            string? sessionId = data.TryGetProperty("sessionId", out var sessionIdProperty) ? sessionIdProperty.GetString() : null;
            string? sessionFile = data.TryGetProperty("sessionFile", out var sessionFileProperty) ? sessionFileProperty.GetString() : null;

            state.Session = new AgentExecutionSession(
                Backend: "pi",
                SessionId: sessionId,
                SessionFile: sessionFile,
                WorkingDirectory: state.WorkingDirectory,
                SupportsSupplementalMessages: true);

            state.SessionReady.TrySetResult(state.Session);
            await state.EmitAsync($"\n[pi-session] {sessionId ?? "unknown"}\n");
        }

        if (id != null && state.PendingCommands.TryRemove(id, out var pending))
        {
            pending.TrySetResult(new RpcCommandResponse(command, success, error, dataJson));
        }
        else if (!success && !string.IsNullOrWhiteSpace(error))
        {
            await state.EmitAsync($"\n[pi-error] {error}\n");
        }
    }

    private static async Task HandleMessageUpdateAsync(PiExecutionState state, JsonElement root)
    {
        if (!root.TryGetProperty("assistantMessageEvent", out var messageEvent))
        {
            return;
        }

        if (!messageEvent.TryGetProperty("type", out var eventTypeProperty))
        {
            return;
        }

        if (eventTypeProperty.GetString() != "text_delta")
        {
            return;
        }

        if (!messageEvent.TryGetProperty("delta", out var deltaProperty))
        {
            return;
        }

        var delta = deltaProperty.GetString();
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }

        lock (state.SyncRoot)
        {
            state.ActivePromptText.Append(delta);
        }

        await state.EmitAsync(delta);
    }

    private static void CompletePrompt(PiExecutionState state)
    {
        TaskCompletionSource<string>? promptResult;
        string text;

        lock (state.SyncRoot)
        {
            promptResult = state.ActivePromptResult;
            if (promptResult == null)
            {
                return;
            }

            text = state.ActivePromptText.ToString();
            state.ActivePromptResult = null;
            state.ActivePromptText.Clear();
        }

        promptResult.TrySetResult(text);
    }

    private static void CompletePromptWithError(PiExecutionState state, string error)
    {
        TaskCompletionSource<string>? promptResult;

        lock (state.SyncRoot)
        {
            promptResult = state.ActivePromptResult;
            state.ActivePromptResult = null;
            state.ActivePromptText.Clear();
        }

        promptResult?.TrySetException(new InvalidOperationException(error));
    }

    private static void FailPendingCommands(PiExecutionState state, string error)
    {
        foreach (var pair in state.PendingCommands.ToArray())
        {
            if (state.PendingCommands.TryRemove(pair.Key, out var pending))
            {
                pending.TrySetException(new InvalidOperationException(error));
            }
        }
    }

    private string? BuildSessionName(Guid executionId)
    {
        if (string.IsNullOrWhiteSpace(SessionNamePrefix))
        {
            return null;
        }

        return $"{SessionNamePrefix}-{executionId:N}";
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record RpcCommandResponse(string? Command, bool Success, string? Error, string? DataJson);

    private sealed class PiExecutionState
    {
        public PiExecutionState(Process process, string workingDirectory, Func<string, Task> onChunk, ILogger logger)
        {
            Process = process;
            WorkingDirectory = workingDirectory;
            OnChunk = onChunk;
            Logger = logger;
            Session = new AgentExecutionSession("pi", null, null, workingDirectory, true);
        }

        public Process Process { get; }
        public string WorkingDirectory { get; }
        public Func<string, Task> OnChunk { get; }
        public ILogger Logger { get; }
        public object SyncRoot { get; } = new();
        public SemaphoreSlim WriteLock { get; } = new(1, 1);
        public ConcurrentDictionary<string, TaskCompletionSource<RpcCommandResponse>> PendingCommands { get; } = new();
        public TaskCompletionSource<AgentExecutionSession> SessionReady { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string>? ActivePromptResult { get; set; }
        public StringBuilder ActivePromptText { get; } = new();
        public AgentExecutionSession Session { get; set; }
        public Task? StdoutPump { get; set; }
        public Task? StderrPump { get; set; }

        public async Task EmitAsync(string chunk)
        {
            try
            {
                await OnChunk(chunk);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Ignoring pi chunk sink failure");
            }
        }
    }
}
