using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

public class EfTaskRepository : ITaskRepository
{
    private readonly ChronoDbContext _context;
    private readonly ILogger<EfTaskRepository> _logger;

    public EfTaskRepository(ChronoDbContext context, ILogger<EfTaskRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ScheduledTask> CreateAsync(CreateTaskDto dto)
    {
        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            CronExpression = dto.CronExpression,
            RepositoryUrl = dto.RepositoryUrl,
            BaseBranch = dto.BaseBranch,
            BranchStrategy = dto.BranchStrategy,
            MaxRuntimeSeconds = dto.MaxRuntimeSeconds,
            MaxFileChanges = dto.MaxFileChanges,
            IsEnabled = dto.IsEnabled,
            WorkflowVersion = WorkflowDefinitionFactory.CurrentVersion,
            WorkflowDefinitionJson = dto.WorkflowDefinitionJson,
            DefaultInputsJson = dto.DefaultInputsJson,
            RuntimeBackend = dto.RuntimeBackend,
            MaxConcurrentRuns = dto.MaxConcurrentRuns,
            NodeFailurePolicyJson = dto.NodeFailurePolicyJson,
            CreatedAt = DateTime.UtcNow,
            LastStatus = Models.TaskStatus.Pending,
            SchedulerStatus = SchedulerStatus.Idle
        };

        _context.ScheduledTasks.Add(task);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created task {TaskId}: {TaskName}", task.Id, task.Name);
        return task;
    }

    public async Task<ScheduledTask?> GetByIdAsync(Guid id)
    {
        return await _context.ScheduledTasks.FindAsync(id);
    }

    public async Task<List<ScheduledTask>> GetAllAsync()
    {
        return await _context.ScheduledTasks.ToListAsync();
    }

    public async Task<ScheduledTask> UpdateAsync(Guid id, UpdateTaskDto dto)
    {
        var task = await _context.ScheduledTasks.FindAsync(id)
                   ?? throw new KeyNotFoundException($"Task {id} not found");

        var workflowChanged = false;

        if (dto.Name != null) task.Name = dto.Name;
        if (dto.CronExpression != null) task.CronExpression = dto.CronExpression;
        if (dto.RepositoryUrl != null) task.RepositoryUrl = dto.RepositoryUrl;
        if (dto.BaseBranch != null) task.BaseBranch = dto.BaseBranch;
        if (dto.BranchStrategy.HasValue) task.BranchStrategy = dto.BranchStrategy.Value;
        if (dto.MaxRuntimeSeconds.HasValue) task.MaxRuntimeSeconds = dto.MaxRuntimeSeconds.Value;
        if (dto.MaxFileChanges.HasValue) task.MaxFileChanges = dto.MaxFileChanges.Value;
        if (dto.IsEnabled.HasValue) task.IsEnabled = dto.IsEnabled.Value;
        if (dto.DefaultInputsJson != null) task.DefaultInputsJson = dto.DefaultInputsJson;
        if (dto.RuntimeBackend != null) task.RuntimeBackend = dto.RuntimeBackend;
        if (dto.MaxConcurrentRuns.HasValue) task.MaxConcurrentRuns = dto.MaxConcurrentRuns.Value;
        if (dto.NodeFailurePolicyJson != null) task.NodeFailurePolicyJson = dto.NodeFailurePolicyJson;

        if (dto.WorkflowDefinitionJson != null
            && !string.Equals(dto.WorkflowDefinitionJson, task.WorkflowDefinitionJson, StringComparison.Ordinal))
        {
            task.WorkflowDefinitionJson = dto.WorkflowDefinitionJson;
            task.WorkflowVersion++;
            workflowChanged = true;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated task {TaskId}{Workflow}", id, workflowChanged ? " (workflow bumped)" : "");
        return task;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var task = await _context.ScheduledTasks.FindAsync(id);
        if (task == null)
        {
            return false;
        }

        _context.ScheduledTasks.Remove(task);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted task {TaskId}", id);
        return true;
    }

    public async Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null)
    {
        var task = await _context.ScheduledTasks.FindAsync(id);
        if (task == null)
        {
            return;
        }

        task.LastRunAt = DateTime.UtcNow;
        task.LastStatus = status;
        task.LastError = error;
        await _context.SaveChangesAsync();
    }

    public async Task UpdateSchedulerStateAsync(Guid taskId, DateTime? nextRunAt, DateTime? lastQueuedAt, string schedulerStatus, DateTime? heartbeatAt)
    {
        var task = await _context.ScheduledTasks.FindAsync(taskId);
        if (task == null)
        {
            return;
        }

        // null is meaningful: UnscheduleTaskAsync / SyncTaskAsync(disabled) pass null
        // to clear NextRunAt / LastQueuedAt so the task stops appearing in GetDueTasksAsync.
        task.NextRunAt = nextRunAt;
        task.LastQueuedAt = lastQueuedAt;
        task.SchedulerStatus = schedulerStatus;
        if (heartbeatAt.HasValue) task.SchedulerHeartbeatAt = heartbeatAt.Value;

        await _context.SaveChangesAsync();
    }

    public async Task<List<ScheduledTask>> GetDueTasksAsync(DateTime now)
    {
        return await _context.ScheduledTasks
            .Where(t => t.IsEnabled
                        && t.NextRunAt != null
                        && t.NextRunAt <= now)
            .ToListAsync();
    }
}
