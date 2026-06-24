using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

public class WorkflowMigrationTests
{
    private static ChronoDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ChronoDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
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
    public async Task ApplyBackfill_FillsEmptyWorkflowWithReviewGraph_AndEmbedsPrompt()
    {
        await using var db = CreateDb("backfill_review");
        var taskId = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(taskId));
        await db.SaveChangesAsync();

        var legacy = new[] { new WorkflowMigration.LegacyTask(taskId, "Fix the failing tests", true) };
        await WorkflowMigration.ApplyBackfillAsync(db, legacy);

        var task = await db.ScheduledTasks.FindAsync(taskId);
        Assert.NotNull(task);
        Assert.NotEqual("{}", task!.WorkflowDefinitionJson);

        Assert.True(WorkflowDefinitionValidator.IsValid(task.WorkflowDefinitionJson, out var error), error);

        var def = WorkflowDefinitionSerializer.Deserialize(task.WorkflowDefinitionJson)!;
        Assert.Contains(def.Nodes, n => n is ApprovalGateWorkflowNode);
        var plan = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "plan");
        Assert.Contains("Fix the failing tests", plan.PromptTemplate);
        Assert.Equal(WorkflowDefinitionFactory.CurrentVersion, task.WorkflowVersion);
        Assert.Equal(WorkflowBackend.Pi, task.RuntimeBackend);
    }

    [Fact]
    public async Task ApplyBackfill_NoReview_OmitsApprovalGate()
    {
        await using var db = CreateDb("backfill_noreview");
        var taskId = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(taskId));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(taskId, "do thing", false)
        });

        var task = await db.ScheduledTasks.FindAsync(taskId);
        var def = WorkflowDefinitionSerializer.Deserialize(task!.WorkflowDefinitionJson)!;
        Assert.DoesNotContain(def.Nodes, n => n is ApprovalGateWorkflowNode);
        Assert.Equal(
            new[] { "start", "prepare_workspace", "plan", "execute", "commit", "pr", "end" },
            def.Nodes.Select(n => n.NodeId).ToArray());
    }

    [Fact]
    public async Task ApplyBackfill_DoesNotOverwriteExistingWorkflow()
    {
        await using var db = CreateDb("backfill_keep");
        var taskId = Guid.NewGuid();
        var existing = WorkflowDefinitionSerializer.Serialize(
            WorkflowDefinitionFactory.CreateDefault(false, "original"));
        db.ScheduledTasks.Add(LegacyRow(taskId, existing));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, new[]
        {
            new WorkflowMigration.LegacyTask(taskId, "should not be used", true)
        });

        var task = await db.ScheduledTasks.FindAsync(taskId);
        Assert.Equal(existing, task!.WorkflowDefinitionJson);
    }

    [Fact]
    public async Task ApplyBackfill_SkipsTasksWithoutLegacyCapture()
    {
        await using var db = CreateDb("backfill_nolegacy");
        var taskId = Guid.NewGuid();
        db.ScheduledTasks.Add(LegacyRow(taskId));
        await db.SaveChangesAsync();

        await WorkflowMigration.ApplyBackfillAsync(db, Array.Empty<WorkflowMigration.LegacyTask>());

        var task = await db.ScheduledTasks.FindAsync(taskId);
        Assert.Equal("{}", task!.WorkflowDefinitionJson);
    }

    [Fact]
    public async Task ReadLegacyTasks_ReturnsEmpty_OnFreshInMemoryDb()
    {
        await using var db = CreateDb("backfill_readempty");
        var legacy = await WorkflowMigration.ReadLegacyTasksAsync(db);
        Assert.Empty(legacy);
    }
}
