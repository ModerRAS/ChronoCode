using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using Cronos;

namespace ChronoCode.Services;

/// <summary>
/// Scheduler state management: cron next-run computation, manual triggers, and
/// queue snapshots. Singleton that resolves scoped repos/run-service per operation
/// via <see cref="IServiceScopeFactory"/> (never holds scoped refs). The actual
/// dispatch loop lives in <see cref="SchedulerBackgroundService"/>.
/// </summary>
public sealed class AppSchedulerService : ISchedulerService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppSchedulerService> _logger;

    public AppSchedulerService(
        IServiceScopeFactory scopeFactory,
        ILogger<AppSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task SyncTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

        if (!task.IsEnabled)
        {
            await taskRepo.UpdateSchedulerStateAsync(task.Id, null, task.LastQueuedAt, SchedulerStatus.Paused, DateTime.UtcNow);
            return;
        }

        var next = ComputeNextOccurrence(task.CronExpression);
        await taskRepo.UpdateSchedulerStateAsync(task.Id, next, task.LastQueuedAt, SchedulerStatus.Idle, DateTime.UtcNow);
    }

    public async Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        await taskRepo.UpdateSchedulerStateAsync(taskId, null, null, SchedulerStatus.Paused, DateTime.UtcNow);
    }

    public async Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        ScheduledTask task;
        using (var scope = _scopeFactory.CreateScope())
        {
            var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var execRepo = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
            task = await taskRepo.GetByIdAsync(taskId)
                ?? throw new InvalidOperationException($"Task {taskId} not found.");

            // Honor the same MaxConcurrentRuns cap the scheduled dispatcher enforces
            // (SchedulerBackgroundService.TickAsync). Without this, a manual trigger
            // could start an unbounded number of concurrent runs for a single task.
            var activeCount = await execRepo.CountActiveRunsAsync(task.Id);
            if (activeCount >= task.MaxConcurrentRuns)
            {
                _logger.LogWarning(
                    "Manual trigger of task {TaskId} skipped: {Active}/{Max} concurrent runs already active",
                    taskId, activeCount, task.MaxConcurrentRuns);
                return;
            }

            await taskRepo.UpdateSchedulerStateAsync(task.Id, task.NextRunAt, DateTime.UtcNow, SchedulerStatus.Queued, DateTime.UtcNow);
        }

        // Run on a background thread with its own scope so the request can return
        // immediately and the scoped DbContext outlives this method.
        _ = Task.Run(async () =>
        {
            try
            {
                using var runScope = _scopeFactory.CreateScope();
                var runService = runScope.ServiceProvider.GetRequiredService<IWorkflowRunService>();
                await runService.StartRunAsync(task, WorkflowTriggerSource.Manual, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual trigger of task {TaskId} failed", taskId);
            }
        }, CancellationToken.None);
    }

    public async Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        return await taskRepo.GetAllAsync();
    }

    public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken cancellationToken = default)
    {
        return Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
            var task = await taskRepo.GetByIdAsync(taskId);
            if (task == null || string.IsNullOrWhiteSpace(task.CronExpression) || !task.IsEnabled)
            {
                return new List<DateTime>();
            }

            var times = new List<DateTime>();
            var from = DateTimeOffset.Now;
            try
            {
                var expr = CronExpression.Parse(task.CronExpression);
                for (var i = 0; i < count; i++)
                {
                    var next = expr.GetNextOccurrence(from, TimeZoneInfo.Local);
                    if (next == null) break;
                    times.Add(next.Value.UtcDateTime);
                    from = next.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute next run times for task {TaskId}", taskId);
            }
            return times;
        }, cancellationToken);
    }

    public async Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        using var scope = _scopeFactory.CreateScope();
        var taskRepo = scope.ServiceProvider.GetRequiredService<ITaskRepository>();
        var execRepo = scope.ServiceProvider.GetRequiredService<IExecutionRepository>();

        var tasks = await taskRepo.GetAllAsync();
        var dueTasks = tasks.Where(t => t.IsEnabled && t.NextRunAt != null && t.NextRunAt <= now).ToList();
        var retryable = await execRepo.GetRetryableNodeExecutionsAsync(now);
        var activeRuns = await execRepo.GetActiveRunsAsync();

        var items = new List<QueueItemDto>();

        foreach (var node in retryable)
        {
            items.Add(new QueueItemDto
            {
                Kind = "node_retry",
                ExecutionId = node.ExecutionId,
                NodeExecutionId = node.Id,
                NextRetryAt = node.NextRetryAt
            });
        }

        foreach (var task in dueTasks)
        {
            items.Add(new QueueItemDto
            {
                Kind = "new_run",
                TaskId = task.Id,
                TaskName = task.Name,
                NextRetryAt = task.NextRunAt
            });
        }

        return new SchedulerQueueSnapshotDto
        {
            NewRunItems = dueTasks.Count,
            NodeRetryItems = retryable.Count,
            ActiveRuns = activeRuns.Count,
            Items = items
        };
    }

    internal static DateTime? ComputeNextOccurrence(string cronExpression)
    {
        if (string.IsNullOrWhiteSpace(cronExpression)) return null;
        try
        {
            var expr = CronExpression.Parse(cronExpression);
            var next = expr.GetNextOccurrence(DateTimeOffset.Now, TimeZoneInfo.Local);
            return next?.UtcDateTime;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
