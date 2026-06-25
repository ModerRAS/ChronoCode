using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Additional ChronoDbContext model-configuration tests and WorkflowMigration edge cases.
/// </summary>
public class DbContextAndMigrationTests
{
    // ---- ChronoDbContext model configuration ----

    [Fact]
    public void Model_ScheduledTask_HasKeyWithNoAutoGeneration()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(ScheduledTask))!;
        var key = entity.FindPrimaryKey()!;
        var idProp = Assert.Single(key.Properties);
        Assert.Equal(nameof(ScheduledTask.Id), idProp.Name);
        Assert.Equal(ValueGenerated.Never, idProp.ValueGenerated);
    }

    [Fact]
    public void Model_WorkflowNodeExecution_HasForeignKeyToTaskExecution()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(WorkflowNodeExecution))!;
        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(TaskExecution), fk.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(WorkflowNodeExecution.ExecutionId), Assert.Single(fk.Properties).Name);
    }

    [Fact]
    public void Model_TaskExecution_HasStatusProperty()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(TaskExecution))!;
        var statusProp = entity.FindProperty(nameof(TaskExecution.Status));
        Assert.NotNull(statusProp);
    }

    [Fact]
    public void Model_ScheduledTask_HasBranchStrategyProperty()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(ScheduledTask))!;
        var bsProp = entity.FindProperty(nameof(ScheduledTask.BranchStrategy));
        Assert.NotNull(bsProp);
    }

    [Fact]
    public void Model_WorkflowNodeExecution_HasStatusProperty()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var entity = ctx.Model.FindEntityType(typeof(WorkflowNodeExecution))!;
        var statusProp = entity.FindProperty(nameof(WorkflowNodeExecution.Status));
        Assert.NotNull(statusProp);
    }

    [Fact]
    public async Task DbContext_CanAddAndRetrieveScheduledTask()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = "Integration Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = "{}",
            CreatedAt = DateTime.UtcNow,
            LastStatus = TaskStatus.Pending
        };
        ctx.ScheduledTasks.Add(task);
        await ctx.SaveChangesAsync();

        var found = await ctx.ScheduledTasks.FindAsync(task.Id);
        Assert.NotNull(found);
        Assert.Equal("Integration Test", found!.Name);
    }

    [Fact]
    public async Task DbContext_CanAddAndRetrieveNodeExecution()
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var ctx = new ChronoDbContext(options);

        var execId = Guid.NewGuid();
        ctx.TaskExecutions.Add(new TaskExecution
        {
            Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running,
            TriggerSource = "manual", StartedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        ctx.WorkflowNodeExecutions.Add(new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "start",
            NodeType = "start", Status = WorkflowNodeStatus.Completed
        });
        await ctx.SaveChangesAsync();

        var nodes = await ctx.WorkflowNodeExecutions
            .Where(n => n.ExecutionId == execId)
            .ToListAsync();
        Assert.Single(nodes);
    }

    // ---- WorkflowMigration additional edge cases ----

    private static ChronoDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(name).Options;
        return new ChronoDbContext(options);
    }

    private static ScheduledTask LegacyRow(Guid id, string workflowJson = "{}") => new()
    {
        Id = id,
        Name = "legacy-task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/owner/repo",
        WorkflowDefinitionJson = workflowJson,
        NodeFailurePolicyJson = "{}",
        CreatedAt = DateTime.UtcNow,
        LastStatus = TaskStatus.Pending
    };

    [Fact]
    public async Task ApplyBackfill_MultipleTasks_AllGetWorkflows()
    {
        await using var db = CreateDb("backfill_multi");
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(id1));
        db.ScheduledTasks.Add(LegacyRow(id2));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(id1, "task one prompt", true),
            new WorkflowMigration.LegacyTask(id2, "task two prompt", false)
        });

        var t1 = await db.ScheduledTasks.FindAsync(id1);
        var t2 = await db.ScheduledTasks.FindAsync(id2);
        Assert.NotEqual("{}", t1!.WorkflowDefinitionJson);
        Assert.NotEqual("{}", t2!.WorkflowDefinitionJson);
        Assert.NotEqual(t1.WorkflowDefinitionJson, t2.WorkflowDefinitionJson);
    }

    [Fact]
    public async Task ApplyBackfill_NullPrompt_StillCreatesValidWorkflow()
    {
        await using var db = CreateDb("backfill_nullprompt");
        var id = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(id));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(id, null!, false)
        });

        var task = await db.ScheduledTasks.FindAsync(id);
        Assert.NotEqual("{}", task!.WorkflowDefinitionJson);
        Assert.True(WorkflowDefinitionValidator.IsValid(task.WorkflowDefinitionJson, out _));
    }

    [Fact]
    public async Task ApplyBackfill_EmptyPrompt_StillCreatesValidWorkflow()
    {
        await using var db = CreateDb("backfill_emptyprompt");
        var id = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(id));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(id, "", false)
        });

        var task = await db.ScheduledTasks.FindAsync(id);
        Assert.NotEqual("{}", task!.WorkflowDefinitionJson);
    }

    [Fact]
    public async Task ApplyBackfill_WithReview_ProducesValidWorkflowGraph()
    {
        await using var db = CreateDb("backfill_review_valid");
        var id = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(id));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(id, "Review my code", true)
        });

        var task = await db.ScheduledTasks.FindAsync(id);
        var def = WorkflowDefinitionSerializer.Deserialize(task!.WorkflowDefinitionJson)!;

        // Should have start → prepare → plan → approval → execute → commit → pr → end
        Assert.Equal("start", def.StartNodeId);
        var nodeIds = def.Nodes.Select(n => n.NodeId).ToArray();
        Assert.Contains("review", nodeIds);
        Assert.Contains("plan", nodeIds);
        Assert.Contains("execute", nodeIds);

        // The plan agent should embed the prompt
        var plan = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "plan");
        Assert.Contains("Review my code", plan.PromptTemplate);
    }

    [Fact]
    public async Task ApplyBackfill_MixedLegacyAndNonLegacy_OnlyMigratesLegacy()
    {
        await using var db = CreateDb("backfill_mixed");
        var legacyId = Guid.NewGuid();
        var modernId = Guid.NewGuid();
        var modernWorkflow = WorkflowDefinitionSerializer.Serialize(
            WorkflowDefinitionFactory.CreateDefault(false, "already set"));

        db.ScheduledTasks.Add(LegacyRow(legacyId));
        db.ScheduledTasks.Add(LegacyRow(modernId, modernWorkflow));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(legacyId, "migrate me", false)
        });

        var migrated = await db.ScheduledTasks.FindAsync(legacyId);
        var unchanged = await db.ScheduledTasks.FindAsync(modernId);

        Assert.NotEqual("{}", migrated!.WorkflowDefinitionJson);
        Assert.Equal(modernWorkflow, unchanged!.WorkflowDefinitionJson);
    }
}
