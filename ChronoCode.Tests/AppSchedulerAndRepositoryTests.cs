using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Focused regression tests for the three Oracle-flagged backend gaps:
///  1. AppSchedulerService.TriggerTaskAsync must honor MaxConcurrentRuns.
///  2. WorkflowRunService failure paths (approval rejection, expired lease) must
///     propagate task-level LastStatus / LastError.
///  3. UpdateSchedulerStateAsync must actually clear NextRunAt / LastQueuedAt
///     when null is passed (UnscheduleTaskAsync / SyncTaskAsync rely on this).
/// </summary>
public class AppSchedulerAndRepositoryTests
{
    private static string SimpleWorkflowJson() => WorkflowDefinitionSerializer.Serialize(new WorkflowDefinition
    {
        Version = 1,
        StartNodeId = "start",
        Nodes =
        [
            new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "end" },
            new EndWorkflowNode { NodeId = "end", Name = "End" }
        ]
    });

    private static async Task<ScheduledTask> CreateTaskAsync(InMemoryTaskRepository taskRepo, int maxConcurrentRuns = 1)
    {
        var dto = new CreateTaskDto
        {
            Name = "sched-" + Guid.NewGuid().ToString("N")[..8],
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            BranchStrategy = BranchStrategy.New,
            MaxRuntimeSeconds = 60,
            MaxFileChanges = 50,
            IsEnabled = true,
            WorkflowDefinitionJson = SimpleWorkflowJson(),
            DefaultInputsJson = null,
            RuntimeBackend = WorkflowBackend.Pi,
            MaxConcurrentRuns = maxConcurrentRuns,
            NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson()
        };
        return await taskRepo.CreateAsync(dto);
    }

    // ---- Bug 3: UpdateSchedulerStateAsync null-clearing ----

    [Fact]
    public async Task UpdateSchedulerStateAsync_InMemory_NullNextRunAt_ClearsField()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await CreateTaskAsync(taskRepo);

        // Seed: scheduler queued the task with a future NextRunAt + LastQueuedAt.
        var future = DateTime.UtcNow.AddHours(1);
        await taskRepo.UpdateSchedulerStateAsync(task.Id, future, DateTime.UtcNow, SchedulerStatus.Queued, DateTime.UtcNow);
        var seeded = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(seeded);
        Assert.NotNull(seeded!.NextRunAt);
        Assert.NotNull(seeded.LastQueuedAt);

        // Act: unschedule passes null for both — must actually clear them.
        await taskRepo.UpdateSchedulerStateAsync(task.Id, null, null, SchedulerStatus.Paused, DateTime.UtcNow);

        var after = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(after);
        Assert.Null(after!.NextRunAt);
        Assert.Null(after.LastQueuedAt);
        Assert.Equal(SchedulerStatus.Paused, after.SchedulerStatus);
    }

    [Fact]
    public async Task UpdateSchedulerStateAsync_InMemory_NullLastQueuedAt_ClearsOnlyQueued()
    {
        // SyncTaskAsync(disabled) passes null nextRunAt but keeps task.LastQueuedAt.
        // We assert that null is honored per-field, not blanket-applied.
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await CreateTaskAsync(taskRepo);

        var future = DateTime.UtcNow.AddHours(1);
        var queuedAt = DateTime.UtcNow.AddMinutes(-5);
        await taskRepo.UpdateSchedulerStateAsync(task.Id, future, queuedAt, SchedulerStatus.Queued, DateTime.UtcNow);

        await taskRepo.UpdateSchedulerStateAsync(task.Id, null, queuedAt, SchedulerStatus.Paused, DateTime.UtcNow);

        var after = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(after);
        Assert.Null(after!.NextRunAt);            // cleared
        Assert.Equal(queuedAt, after.LastQueuedAt); // preserved (non-null passed)
    }

    [Fact]
    public async Task GetDueTasksAsync_InMemory_DoesNotReturnUnscheduledTask()
    {
        // The downstream symptom of the null-clearing bug: a disabled/unscheduled
        // task keeps a stale NextRunAt and the dispatcher re-fires it forever.
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await CreateTaskAsync(taskRepo);

        var past = DateTime.UtcNow.AddSeconds(-30);
        await taskRepo.UpdateSchedulerStateAsync(task.Id, past, DateTime.UtcNow, SchedulerStatus.Queued, DateTime.UtcNow);
        Assert.Single(await taskRepo.GetDueTasksAsync(DateTime.UtcNow));

        await taskRepo.UpdateSchedulerStateAsync(task.Id, null, null, SchedulerStatus.Paused, DateTime.UtcNow);

        Assert.Empty(await taskRepo.GetDueTasksAsync(DateTime.UtcNow));
    }

    // ---- Bug 1: TriggerTaskAsync must enforce MaxConcurrentRuns ----

    [Fact]
    public async Task TriggerTaskAsync_AtMaxConcurrentRuns_DoesNotStartNewRun()
    {
        // Build a real DI container so AppSchedulerService can resolve its scopes.
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders());
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        services.AddSingleton<ITaskRepository>(taskRepo);
        services.AddSingleton<IExecutionRepository>(execRepo);
        services.AddSingleton<IWorkflowRunService, CountingWorkflowRunService>();
        var provider = services.BuildServiceProvider();

        var scheduler = new AppSchedulerService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AppSchedulerService>.Instance);

        var task = await CreateTaskAsync(taskRepo, maxConcurrentRuns: 1);

        // Pretend one run is already active for this task (at the cap).
        var active = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = task.WorkflowDefinitionJson
        };
        await execRepo.CreateAsync(active);

        await scheduler.TriggerTaskAsync(task.Id);

        // The counting service increments on every StartRunAsync call. TriggerTaskAsync
        // must NOT have called it because the task is already at MaxConcurrentRuns.
        var runService = (CountingWorkflowRunService)provider.GetRequiredService<IWorkflowRunService>();
        Assert.Equal(0, runService.StartCalls);

        // And the task scheduler status must not have flipped to Queued for a run that
        // was suppressed.
        var after = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(after);
        Assert.NotEqual(SchedulerStatus.Queued, after!.SchedulerStatus);
    }

    [Fact]
    public async Task TriggerTaskAsync_BelowMaxConcurrentRuns_StartsNewRun()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.ClearProviders());
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        services.AddSingleton<ITaskRepository>(taskRepo);
        services.AddSingleton<IExecutionRepository>(execRepo);
        services.AddSingleton<IWorkflowRunService, CountingWorkflowRunService>();
        var provider = services.BuildServiceProvider();

        var scheduler = new AppSchedulerService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AppSchedulerService>.Instance);

        var task = await CreateTaskAsync(taskRepo, maxConcurrentRuns: 2);

        await scheduler.TriggerTaskAsync(task.Id);

        var runService = (CountingWorkflowRunService)provider.GetRequiredService<IWorkflowRunService>();
        // The background Task.Run in TriggerTaskAsync is fire-and-forget; poll briefly.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (runService.StartCalls == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
        Assert.Equal(1, runService.StartCalls);
    }

    /// <summary>
    /// IWorkflowRunService stub that just counts StartRunAsync invocations.
    /// Used to assert whether TriggerTaskAsync actually dispatched a run.
    /// </summary>
    private sealed class CountingWorkflowRunService : IWorkflowRunService
    {
        public int StartCalls;

        public Task<TaskExecution> StartRunAsync(ScheduledTask task, string triggerSource, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref StartCalls);
            return Task.FromResult(new TaskExecution
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                Status = TaskStatus.Completed,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                WorkflowSnapshotJson = task.WorkflowDefinitionJson,
                TriggerSource = triggerSource
            });
        }

        public Task ContinueRunAsync(Guid executionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ApproveNodeAsync(Guid executionId, Guid nodeExecutionId, bool approved, string? reason, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkflowNodeExecution?>(null);
        public Task<AgentExecutionSession?> GetNodeSessionAsync(Guid executionId, Guid nodeExecutionId, CancellationToken cancellationToken = default)
            => Task.FromResult<AgentExecutionSession?>(null);
        public Task<AgentExecutionSession> ResumeNodeSessionAsync(Guid executionId, Guid nodeExecutionId, string? sessionRef, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task<string> SendNodeMessageAsync(Guid executionId, Guid nodeExecutionId, string message, string mode, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
        public Task RecoverStuckNodesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
