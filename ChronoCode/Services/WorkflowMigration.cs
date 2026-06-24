using ChronoCode.Data;
using ChronoCode.Models.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

/// <summary>
/// Smooth backfill of legacy single-prompt tasks into the workflow graph DSL.
/// Reads the old Prompt/RequirePlanReview columns (pre-migration, via raw SQL) and,
/// after the schema migration adds the workflow columns, fills WorkflowDefinitionJson
/// with the default migrated graph (start -> prepare_workspace -> agent(plan) -> [approval_gate] ->
/// agent(execute) -> commit_changes -> create_pull_request -> end).
/// </summary>
public static class WorkflowMigration
{
    public sealed record LegacyTask(Guid Id, string Prompt, bool RequirePlanReview);

    /// <summary>Read legacy Prompt/RequirePlanReview columns before the migration drops them. No-op on fresh DBs.</summary>
    public static async Task<List<LegacyTask>> ReadLegacyTasksAsync(ChronoDbContext db, CancellationToken ct = default)
    {
        try
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
            {
                await conn.OpenAsync(ct);
            }

            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT ""Id"", ""Prompt"", ""RequirePlanReview"" FROM ""ScheduledTasks""";
                var list = new List<LegacyTask>();
                using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var id = reader.GetGuid(0);
                    var prompt = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var review = reader.IsDBNull(2) ? false : reader.GetBoolean(2);
                    list.Add(new LegacyTask(id, prompt ?? string.Empty, review));
                }
                return list;
            }
            finally
            {
                if (conn.State == System.Data.ConnectionState.Open)
                {
                    await conn.CloseAsync();
                }
            }
        }
        catch
        {
            return new List<LegacyTask>();
        }
    }

    /// <summary>
    /// Fill blank/empty/invalid WorkflowDefinitionJson for legacy tasks using captured prompt/review state.
    /// The migration renames the legacy <c>Prompt</c> column to <c>WorkflowDefinitionJson</c>, so legacy
    /// prompt TEXT ends up as the workflow JSON value. Detect that by trying to parse it as a
    /// <see cref="WorkflowDefinition"/>; if parsing fails or validation fails, backfill it.
    /// </summary>
    public static async Task ApplyBackfillAsync(ChronoDbContext db, IEnumerable<LegacyTask> legacy, CancellationToken ct = default)
    {
        var byId = legacy.ToDictionary(l => l.Id);
        var tasks = await db.ScheduledTasks.ToListAsync(ct);
        var changed = false;

        foreach (var task in tasks)
        {
            if (IsValidWorkflowDefinition(task.WorkflowDefinitionJson))
            {
                continue;
            }

            if (!byId.TryGetValue(task.Id, out var legacyTask))
            {
                continue;
            }

            task.WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(legacyTask.RequirePlanReview, legacyTask.Prompt);
            task.WorkflowVersion = WorkflowDefinitionFactory.CurrentVersion;
            task.NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson();
            task.RuntimeBackend ??= WorkflowBackend.Pi;
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static bool IsValidWorkflowDefinition(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return false;
        }

        // The migration renames Prompt -> WorkflowDefinitionJson, so legacy prompt
        // text ends up here. Detect by parsing: valid workflow JSON parses
        // AND validates; anything else (e.g. free-form prompt text) does not.
        if (WorkflowDefinitionSerializer.Deserialize(json) is null)
        {
            return false;
        }

        return WorkflowDefinitionValidator.IsValid(json!, out _);
    }

    /// <summary>Backfill a single in-memory task (used by tests and migration verification).</summary>
    public static void BackfillTask(Models.ScheduledTask task, string? legacyPrompt, bool requirePlanReview)
    {
        task.WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(requirePlanReview, legacyPrompt);
        task.WorkflowVersion = WorkflowDefinitionFactory.CurrentVersion;
        task.NodeFailurePolicyJson = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson();
        task.RuntimeBackend ??= WorkflowBackend.Pi;
    }
}
