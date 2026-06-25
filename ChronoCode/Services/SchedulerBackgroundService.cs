using ChronoCode.Models.Workflow;

namespace ChronoCode.Services;

/// <summary>
/// The sole workflow dispatcher. Every 5s: recover stuck nodes, process node retries
/// (priority), process due tasks, recompute next-run times, update heartbeats.
/// Singleton hosted service: scoped repos/run-service are resolved per operation via
/// <see cref="IServiceProvider.CreateScope"/> (never held as fields).
/// </summary>
public sealed class SchedulerBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SchedulerBackgroundService> _logger;

    private readonly SemaphoreSlim _globalSemaphore;
    private readonly TimeSpan _interval;

    public SchedulerBackgroundService(
        IServiceProvider services,
        ILogger<SchedulerBackgroundService> logger)
    {
        _services = services;
        _logger = logger;

        var maxConcurrent = int.TryParse(services.GetRequiredService<IConfiguration>()["Scheduler:MaxConcurrentRunsGlobal"], out var mc) && mc > 0
            ? mc
            : 4;
        _globalSemaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        _interval = int.TryParse(services.GetRequiredService<IConfiguration>()["Scheduler:PollIntervalSeconds"], out var pi) && pi > 0
            ? TimeSpan.FromSeconds(pi)
            : TimeSpan.FromSeconds(5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler background service starting (interval={Interval})", _interval);

        // Resume active runs and recover stuck nodes on startup.
        await SafeAsync(async () =>
        {
            List<Models.TaskExecution> active;
            using (var scope = _services.CreateScope())
            {
                var execRepo = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
                active = await execRepo.GetActiveRunsAsync();
            }

            foreach (var run in active)
            {
                var runId = run.Id;
                _ = Task.Run(async () =>
                {
                    await _globalSemaphore.WaitAsync(stoppingToken);
                    try
                    {
                        using var scope = _services.CreateScope();
                        var runService = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
                        await runService.ContinueRunAsync(runId, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Startup resume failed for execution {ExecutionId}", runId);
                    }
                    finally
                    {
                        _globalSemaphore.Release();
                    }
                }, stoppingToken);
            }
        }, "startup resume");

        await SafeAsync(async () =>
        {
            using var scope = _services.CreateScope();
            var runService = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
            await runService.RecoverStuckNodesAsync(stoppingToken);
        }, "startup recover");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler tick failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Scheduler background service stopping");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        using var scope = _services.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
        var execRepo = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        // 1) Recover stuck nodes.
        await SafeAsync(() => runService.RecoverStuckNodesAsync(ct), "recover stuck");

        // 2) Process node retries FIRST (priority).
        await SafeAsync(async () =>
        {
            var retryable = await execRepo.GetRetryableNodeExecutionsAsync(now);
            foreach (var node in retryable)
            {
                var execId = node.ExecutionId;
                _ = Task.Run(async () =>
                {
                    await _globalSemaphore.WaitAsync(ct);
                    try
                    {
                        using var retryScope = _services.CreateScope();
                        var scopedRun = retryScope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
                        await scopedRun.ContinueRunAsync(execId, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Node retry continuation failed for execution {ExecutionId}", execId);
                    }
                    finally
                    {
                        _globalSemaphore.Release();
                    }
                }, ct);
            }
        }, "node retries");

        // 3) Process due tasks.
        await SafeAsync(async () =>
        {
            var due = await taskRepo.GetDueTasksAsync(now);
            foreach (var task in due)
            {
                if (!task.IsEnabled) continue;

                var activeCount = await execRepo.CountActiveRunsAsync(task.Id);
                if (activeCount >= task.MaxConcurrentRuns)
                {
                    continue;
                }

                var taskRef = task;
                _ = Task.Run(async () =>
                {
                    await _globalSemaphore.WaitAsync(ct);
                    try
                    {
                        using var runScope = _services.CreateScope();
                        var scopedRun = runScope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
                        await scopedRun.StartRunAsync(taskRef, WorkflowTriggerSource.Scheduled, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Scheduled run failed for task {TaskId}", taskRef.Id);
                    }
                    finally
                    {
                        _globalSemaphore.Release();
                    }
                }, ct);
            }
        }, "due tasks");

        // 4) Recompute NextRunAt for tasks whose NextRunAt passed.
        await SafeAsync(async () =>
        {
            var all = await taskRepo.GetAllAsync();
            foreach (var task in all)
            {
                if (!task.IsEnabled) continue;
                if (task.NextRunAt == null || task.NextRunAt > now) continue;

                var next = AppSchedulerService.ComputeNextOccurrence(task.CronExpression);
                await taskRepo.UpdateSchedulerStateAsync(
                    task.Id, next, task.LastQueuedAt, SchedulerStatus.Idle, DateTime.UtcNow);
            }
        }, "recompute next runs");

        // 5) Heartbeat.
        await SafeAsync(async () =>
        {
            var all = await taskRepo.GetAllAsync();
            foreach (var task in all)
            {
                if (!task.IsEnabled) continue;
                if (task.SchedulerHeartbeatAt == null || (now - task.SchedulerHeartbeatAt.Value).TotalMinutes >= 1)
                {
                    await taskRepo.UpdateSchedulerStateAsync(
                        task.Id, task.NextRunAt, task.LastQueuedAt, task.SchedulerStatus, now);
                }
            }
        }, "heartbeat");
    }

    private async Task SafeAsync(Func<Task> action, string label)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduler step '{Label}' failed", label);
        }
    }
}
