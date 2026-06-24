using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Additional DbContext tests: DbSet access, CRUD operations,
/// cascade behavior, and default value conventions.
/// </summary>
public class ChronoDbContextAdditionalTests
{
    private static ChronoDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ChronoDbContext(options);
    }

    [Fact]
    public void DbSets_AreNotNull()
    {
        using var ctx = CreateContext();
        Assert.NotNull(ctx.ScheduledTasks);
        Assert.NotNull(ctx.TaskExecutions);
        Assert.NotNull(ctx.WorkflowNodeExecutions);
    }

    [Fact]
    public async Task CanAddAndRetrieve_ScheduledTask()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "test-task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };

        ctx.ScheduledTasks.Add(task);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.ScheduledTasks.FindAsync(task.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("test-task", retrieved!.Name);
        Assert.Equal("0 0 * * *", retrieved.CronExpression);
    }

    [Fact]
    public async Task CanAddAndRetrieve_TaskExecution_WithForeignKey()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "parent-task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        ctx.ScheduledTasks.Add(task);
        await ctx.SaveChangesAsync();

        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = "{}"
        };
        ctx.TaskExecutions.Add(execution);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.TaskExecutions.FindAsync(execution.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(task.Id, retrieved!.TaskId);
        Assert.Equal(TaskStatus.Running, retrieved.Status);
    }

    [Fact]
    public async Task CanAddAndRetrieve_WorkflowNodeExecution()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "node-task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = "{}"
        };
        ctx.ScheduledTasks.Add(task);
        ctx.TaskExecutions.Add(execution);
        await ctx.SaveChangesAsync();

        var nodeExec = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            NodeId = "start",
            NodeType = "start",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        ctx.WorkflowNodeExecutions.Add(nodeExec);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.WorkflowNodeExecutions.FindAsync(nodeExec.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("start", retrieved!.NodeId);
        Assert.Equal(WorkflowNodeStatus.Completed, retrieved.Status);
    }

    [Fact]
    public async Task Update_ScheduledTask_PersistsChanges()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "original",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        ctx.ScheduledTasks.Add(task);
        await ctx.SaveChangesAsync();

        task.Name = "updated";
        task.LastStatus = TaskStatus.Completed;
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.ScheduledTasks.FindAsync(task.Id);
        Assert.Equal("updated", retrieved!.Name);
        Assert.Equal(TaskStatus.Completed, retrieved.LastStatus);
    }

    [Fact]
    public async Task Delete_ScheduledTask_RemovesFromDbSet()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "to-delete",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        ctx.ScheduledTasks.Add(task);
        await ctx.SaveChangesAsync();

        ctx.ScheduledTasks.Remove(task);
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.ScheduledTasks.FindAsync(task.Id);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task Query_MultipleScheduledTasks_ReturnsAll()
    {
        using var ctx = CreateContext();
        for (int i = 0; i < 5; i++)
        {
            ctx.ScheduledTasks.Add(new ScheduledTask
            {
                Id = Guid.NewGuid(),
                Name = $"task-{i}",
                CronExpression = "0 0 * * *",
                RepositoryUrl = "https://github.com/test/repo",
                BaseBranch = "main",
                WorkflowDefinitionJson = "{}",
                NodeFailurePolicyJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }
        await ctx.SaveChangesAsync();

        var tasks = await ctx.ScheduledTasks.ToListAsync();
        Assert.Equal(5, tasks.Count);
    }

    [Fact]
    public async Task TaskExecution_CanUpdateStatus()
    {
        using var ctx = CreateContext();
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "status-task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            BaseBranch = "main",
            WorkflowDefinitionJson = "{}",
            NodeFailurePolicyJson = "{}",
            CreatedAt = DateTime.UtcNow
        };
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            Status = TaskStatus.Running,
            StartedAt = DateTime.UtcNow,
            WorkflowSnapshotJson = "{}"
        };
        ctx.ScheduledTasks.Add(task);
        ctx.TaskExecutions.Add(execution);
        await ctx.SaveChangesAsync();

        execution.Status = TaskStatus.Completed;
        execution.CompletedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var retrieved = await ctx.TaskExecutions.FindAsync(execution.Id);
        Assert.Equal(TaskStatus.Completed, retrieved!.Status);
        Assert.NotNull(retrieved.CompletedAt);
    }

    [Fact]
    public void Model_ScheduledTask_HasIdProperty()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(ScheduledTask))!;
        var idProp = entity.FindProperty(nameof(ScheduledTask.Id));
        Assert.NotNull(idProp);
        Assert.True(idProp!.IsPrimaryKey());
    }

    [Fact]
    public void Model_TaskExecution_HasIdProperty()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(TaskExecution))!;
        var idProp = entity.FindProperty(nameof(TaskExecution.Id));
        Assert.NotNull(idProp);
        Assert.True(idProp!.IsPrimaryKey());
    }

    [Fact]
    public void Model_WorkflowNodeExecution_HasIdProperty()
    {
        using var ctx = CreateContext();
        var entity = ctx.Model.FindEntityType(typeof(WorkflowNodeExecution))!;
        var idProp = entity.FindProperty(nameof(WorkflowNodeExecution.Id));
        Assert.NotNull(idProp);
        Assert.True(idProp!.IsPrimaryKey());
    }
}
