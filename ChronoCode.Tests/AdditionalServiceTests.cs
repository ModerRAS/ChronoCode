using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Additional tests for AppSchedulerService, GitService, and OpencodeServerManager.
/// </summary>
public class AdditionalServiceTests
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

    private static async Task<ScheduledTask> CreateTaskAsync(InMemoryTaskRepository repo, string cron = "0 0 * * *", bool enabled = true)
    {
        return await repo.CreateAsync(new CreateTaskDto
        {
            Name = "task-" + Guid.NewGuid().ToString("N")[..8],
            CronExpression = cron,
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            BranchStrategy = BranchStrategy.New,
            MaxRuntimeSeconds = 60,
            MaxFileChanges = 50,
            IsEnabled = enabled,
            WorkflowDefinitionJson = SimpleWorkflowJson(),
            MaxConcurrentRuns = 1,
            NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson()
        });
    }

    private static AppSchedulerService MakeScheduler(InMemoryTaskRepository taskRepo, InMemoryExecutionRepository execRepo)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITaskRepository>(taskRepo);
        services.AddSingleton<IExecutionRepository>(execRepo);
        services.AddSingleton<IWorkspacePreparationService>(new FakeWorkspacePrep());
        var sp = services.BuildServiceProvider();
        return new AppSchedulerService(sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AppSchedulerService>.Instance);
    }

    // ---- AppSchedulerService.GetNextRunTimesAsync ----

    [Fact]
    public async Task GetNextRunTimes_ReturnsEmpty_WhenTaskNotFound()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);

        var times = await scheduler.GetNextRunTimesAsync(Guid.NewGuid(), 5);

        Assert.Empty(times);
    }

    [Fact]
    public async Task GetNextRunTimes_ReturnsEmpty_WhenDisabled()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, enabled: false);

        var times = await scheduler.GetNextRunTimesAsync(task.Id, 5);

        Assert.Empty(times);
    }

    [Fact]
    public async Task GetNextRunTimes_ReturnsEmpty_WhenCronBlank()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, cron: "");

        var times = await scheduler.GetNextRunTimesAsync(task.Id, 5);

        Assert.Empty(times);
    }

    [Fact]
    public async Task GetNextRunTimes_ReturnsScheduledTimes()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, cron: "0 0 * * *");

        var times = await scheduler.GetNextRunTimesAsync(task.Id, 3);

        Assert.Equal(3, times.Count);
        // Each subsequent time should be later
        Assert.True(times[1] > times[0]);
        Assert.True(times[2] > times[1]);
    }

    [Fact]
    public async Task GetNextRunTimes_ReturnsEmpty_WhenCronInvalid()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, cron: "not-a-cron");

        var times = await scheduler.GetNextRunTimesAsync(task.Id, 3);

        Assert.Empty(times);
    }

    // ---- AppSchedulerService.GetScheduledTasksAsync ----

    [Fact]
    public async Task GetScheduledTasks_ReturnsAllTasks()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        await CreateTaskAsync(taskRepo, "0 0 * * *");
        await CreateTaskAsync(taskRepo, "0 12 * * *");

        var tasks = await scheduler.GetScheduledTasksAsync();

        Assert.Equal(2, tasks.Count);
    }

    // ---- AppSchedulerService.GetQueueSnapshotAsync ----

    [Fact]
    public async Task GetQueueSnapshot_ReturnsEmpty_WhenNoTasks()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);

        var snapshot = await scheduler.GetQueueSnapshotAsync();

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Items);
    }

    [Fact]
    public async Task GetQueueSnapshot_IncludesDueTasks()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo);

        // Set nextRun to the past so it's "due"
        await taskRepo.UpdateSchedulerStateAsync(task.Id, DateTime.UtcNow.AddMinutes(-5), null, "idle", null);

        var snapshot = await scheduler.GetQueueSnapshotAsync();

        Assert.NotEmpty(snapshot.Items);
        Assert.Contains(snapshot.Items, i => i.Kind == "new_run");
    }

    // ---- AppSchedulerService.SyncTaskAsync / UnscheduleTaskAsync ----

    [Fact]
    public async Task UnscheduleTask_DoesNotThrow_WhenTaskMissing()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);

        await scheduler.UnscheduleTaskAsync(Guid.NewGuid());
        // Should not throw
    }

    [Fact]
    public async Task SyncTask_UpdatesNextRun_ForEnabledTask()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, "0 0 * * *");

        await scheduler.SyncTaskAsync(task);

        var found = await taskRepo.GetByIdAsync(task.Id);
        Assert.NotNull(found!.NextRunAt);
    }

    [Fact]
    public async Task SyncTask_ClearsNextRun_ForDisabledTask()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = MakeScheduler(taskRepo, execRepo);
        var task = await CreateTaskAsync(taskRepo, "0 0 * * *", enabled: false);

        await scheduler.SyncTaskAsync(task);

        var found = await taskRepo.GetByIdAsync(task.Id);
        Assert.Null(found!.NextRunAt);
    }

    // ---- OpencodeServerManager ----

    private static OpencodeServerManager CreateManager()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new OpencodeServerManager(
            NullLogger<OpencodeServerManager>.Instance,
            configuration,
            new StubHttpClientFactory());
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string? name) => new();
    }

    [Fact]
    public void OpencodeServerManager_Initial_IsServerRunning_False()
    {
        var mgr = CreateManager();
        Assert.False(mgr.IsServerRunning);
        mgr.Dispose();
    }

    [Fact]
    public async Task OpencodeServerManager_StopServer_DoesNotThrow_WhenNotRunning()
    {
        var mgr = CreateManager();
        await mgr.StopServerAsync();
        Assert.False(mgr.IsServerRunning);
        mgr.Dispose();
    }

    // ---- Fake ----

    private sealed class FakeWorkspacePrep : IWorkspacePreparationService
    {
        public Task<WorkspacePreparationResult> PrepareAsync(ScheduledTask task, Guid executionId, CancellationToken cancellationToken = default)
            => Task.FromResult(new WorkspacePreparationResult("/tmp/fake", "chronocode/fake"));
    }
}
