using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Tests for EfExecutionRepository and EfTaskRepository against SQLite in-memory.
/// </summary>
public class EfRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ChronoDbContext _context;

    public EfRepositoryTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = OFF;";
            cmd.ExecuteNonQuery();
        }
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ChronoDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    // ---- EfExecutionRepository ----

    [Fact]
    public async Task ExecRepo_CreateAndGetById()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var exec = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = TaskStatus.Running,
            TriggerSource = "manual",
            StartedAt = DateTime.UtcNow
        };

        await repo.CreateAsync(exec);
        var found = await repo.GetByIdAsync(exec.Id);

        Assert.NotNull(found);
        Assert.Equal(exec.TaskId, found!.TaskId);
        Assert.Equal(TaskStatus.Running, found.Status);
    }

    [Fact]
    public async Task ExecRepo_GetById_ReturnsNull_WhenNotFound()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var found = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task ExecRepo_GetByTaskId_ReturnsExecutions()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var taskId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Completed, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Failed, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Completed, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        var results = await repo.GetByTaskIdAsync(taskId);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task ExecRepo_UpdateAsync_UpdatesStatus()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var exec = new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow };
        await repo.CreateAsync(exec);

        exec.Status = TaskStatus.Completed;
        exec.CompletedAt = DateTime.UtcNow;
        await repo.UpdateAsync(exec);

        var found = await repo.GetByIdAsync(exec.Id);
        Assert.Equal(TaskStatus.Completed, found!.Status);
        Assert.NotNull(found.CompletedAt);
    }

    [Fact]
    public async Task ExecRepo_AddAndGetLogs()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        await repo.AddLogAsync(execId, "INFO", "Starting task");
        await repo.AddLogAsync(execId, "ERROR", "Something failed", "Stack trace here");

        var logs = await repo.GetLogsAsync(execId);
        Assert.Equal(2, logs.Count);
        Assert.Equal("INFO", logs[0].Level);
        Assert.Equal("Starting task", logs[0].Message);
        Assert.Equal("ERROR", logs[1].Level);
        Assert.Equal("Stack trace here", logs[1].Details);
    }

    [Fact]
    public async Task ExecRepo_GetActiveRuns_ReturnsRunningExecutions()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Completed, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "scheduler", StartedAt = DateTime.UtcNow });

        var active = await repo.GetActiveRunsAsync();
        Assert.Equal(2, active.Count);
    }

    [Fact]
    public async Task ExecRepo_CountActiveRuns()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var taskId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Completed, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        var count = await repo.CountActiveRunsAsync(taskId);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ExecRepo_CreateAndGetNodeExecution()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execId,
            NodeId = "start",
            NodeType = "start",
            Status = WorkflowNodeStatus.Completed,
            ScopeKey = "",
            StartedAt = DateTime.UtcNow
        };

        await repo.CreateNodeExecutionAsync(node);
        var found = await repo.GetNodeExecutionAsync(node.Id);

        Assert.NotNull(found);
        Assert.Equal("start", found!.NodeId);
    }

    [Fact]
    public async Task ExecRepo_GetNodeExecutions_ByExecutionId()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "n1", NodeType = "agent", Status = WorkflowNodeStatus.Completed, ScopeKey = "", StartedAt = DateTime.UtcNow });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "n2", NodeType = "agent", Status = WorkflowNodeStatus.Running, ScopeKey = "", StartedAt = DateTime.UtcNow });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = Guid.NewGuid(), NodeId = "n3", NodeType = "agent", Status = WorkflowNodeStatus.Completed, ScopeKey = "", StartedAt = DateTime.UtcNow });

        var nodes = await repo.GetNodeExecutionsAsync(execId);
        Assert.Equal(2, nodes.Count);
    }

    [Fact]
    public async Task ExecRepo_GetRunningNodeExecutions()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "n1", NodeType = "agent", Status = WorkflowNodeStatus.Running, ScopeKey = "", StartedAt = DateTime.UtcNow });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "n2", NodeType = "agent", Status = WorkflowNodeStatus.Completed, ScopeKey = "", StartedAt = DateTime.UtcNow });

        var running = await repo.GetRunningNodeExecutionsAsync();
        Assert.Single(running);
        Assert.Equal("n1", running[0].NodeId);
    }

    [Fact]
    public async Task ExecRepo_UpdateNodeExecution()
    {
        var repo = new EfExecutionRepository(_context, NullLogger<EfExecutionRepository>.Instance);
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, TriggerSource = "manual", StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "n1", NodeType = "agent", Status = WorkflowNodeStatus.Running, ScopeKey = "", StartedAt = DateTime.UtcNow };
        await repo.CreateNodeExecutionAsync(node);

        node.Status = WorkflowNodeStatus.Completed;
        node.CompletedAt = DateTime.UtcNow;
        await repo.UpdateNodeExecutionAsync(node);

        var found = await repo.GetNodeExecutionAsync(node.Id);
        Assert.Equal(WorkflowNodeStatus.Completed, found!.Status);
    }

    // ---- EfTaskRepository ----

    private static CreateTaskDto MakeCreateDto() => new()
    {
        Name = "Test Task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        BranchStrategy = BranchStrategy.New,
        MaxFileChanges = 50,
        MaxRuntimeSeconds = 600,
        WorkflowDefinitionJson = "{}",
        NodeFailurePolicyJson = "{}",
        MaxConcurrentRuns = 1
    };

    [Fact]
    public async Task TaskRepo_CreateAndGetById()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var dto = MakeCreateDto();
        var task = await repo.CreateAsync(dto);

        var found = await repo.GetByIdAsync(task.Id);
        Assert.NotNull(found);
        Assert.Equal("Test Task", found!.Name);
        Assert.Equal("0 0 * * *", found.CronExpression);
    }

    [Fact]
    public async Task TaskRepo_GetById_ReturnsNull_WhenNotFound()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var found = await repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(found);
    }

    [Fact]
    public async Task TaskRepo_GetAll_ReturnsAllTasks()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        await repo.CreateAsync(MakeCreateDto());
        var dto2 = MakeCreateDto();
        dto2.Name = "Task 2";
        await repo.CreateAsync(dto2);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task TaskRepo_Delete_RemovesTask()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var task = await repo.CreateAsync(MakeCreateDto());

        var deleted = await repo.DeleteAsync(task.Id);
        Assert.True(deleted);

        var found = await repo.GetByIdAsync(task.Id);
        Assert.Null(found);
    }

    [Fact]
    public async Task TaskRepo_Delete_ReturnsFalse_WhenNotFound()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var deleted = await repo.DeleteAsync(Guid.NewGuid());
        Assert.False(deleted);
    }

    [Fact]
    public async Task TaskRepo_UpdateLastRun()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var task = await repo.CreateAsync(MakeCreateDto());

        await repo.UpdateLastRunAsync(task.Id, TaskStatus.Completed, null);

        var found = await repo.GetByIdAsync(task.Id);
        Assert.Equal(TaskStatus.Completed, found!.LastStatus);
        Assert.NotNull(found.LastRunAt);
    }

    [Fact]
    public async Task TaskRepo_UpdateSchedulerState()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var task = await repo.CreateAsync(MakeCreateDto());

        var nextRun = DateTime.UtcNow.AddDays(1);
        await repo.UpdateSchedulerStateAsync(task.Id, nextRun, DateTime.UtcNow, "queued", DateTime.UtcNow);

        var found = await repo.GetByIdAsync(task.Id);
        Assert.Equal("queued", found!.SchedulerStatus);
        Assert.NotNull(found.NextRunAt);
    }

    [Fact]
    public async Task TaskRepo_GetDueTasks_ReturnsEnabledWithPastNextRun()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var task = await repo.CreateAsync(MakeCreateDto());

        // Set nextRun to past
        await repo.UpdateSchedulerStateAsync(task.Id, DateTime.UtcNow.AddMinutes(-5), null, "idle", null);

        var due = await repo.GetDueTasksAsync(DateTime.UtcNow);
        Assert.Single(due);
        Assert.Equal(task.Id, due[0].Id);
    }

    [Fact]
    public async Task TaskRepo_GetDueTasks_ExcludesDisabled()
    {
        var repo = new EfTaskRepository(_context, NullLogger<EfTaskRepository>.Instance);
        var dto = MakeCreateDto();
        dto.IsEnabled = false;
        var task = await repo.CreateAsync(dto);

        await repo.UpdateSchedulerStateAsync(task.Id, DateTime.UtcNow.AddMinutes(-5), null, "idle", null);

        var due = await repo.GetDueTasksAsync(DateTime.UtcNow);
        Assert.Empty(due);
    }
}
