using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services;

public interface ITaskRepository
{
    Task<ScheduledTask> CreateAsync(CreateTaskDto dto);
    Task<ScheduledTask?> GetByIdAsync(Guid id);
    Task<List<ScheduledTask>> GetAllAsync();
    Task<ScheduledTask> UpdateAsync(Guid id, UpdateTaskDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null);
    Task UpdateSchedulerStateAsync(Guid taskId, DateTime? nextRunAt, DateTime? lastQueuedAt, string schedulerStatus, DateTime? heartbeatAt);
    Task<List<ScheduledTask>> GetDueTasksAsync(DateTime now);
}

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<ScheduledTask> _tasks = new();
    private readonly ILogger<InMemoryTaskRepository> _logger;
    private readonly object _lock = new();

    public InMemoryTaskRepository(ILogger<InMemoryTaskRepository> logger)
    {
        _logger = logger;
    }

    public Task<ScheduledTask> CreateAsync(CreateTaskDto dto)
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

        lock (_lock)
        {
            _tasks.Add(task);
        }

        _logger.LogInformation("Created task {TaskId}: {TaskName}", task.Id, task.Name);
        return Task.FromResult(task);
    }

    public Task<ScheduledTask?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_tasks.FirstOrDefault(t => t.Id == id));
        }
    }

    public Task<List<ScheduledTask>> GetAllAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_tasks.ToList());
        }
    }

    public Task<ScheduledTask> UpdateAsync(Guid id, UpdateTaskDto dto)
    {
        ScheduledTask task;
        lock (_lock)
        {
            task = _tasks.FirstOrDefault(t => t.Id == id) ?? throw new KeyNotFoundException($"Task {id} not found");
        }

        if (dto.Name != null) task.Name = dto.Name;
        if (dto.CronExpression != null) task.CronExpression = dto.CronExpression;
        if (dto.RepositoryUrl != null) task.RepositoryUrl = dto.RepositoryUrl;
        if (dto.BaseBranch != null) task.BaseBranch = dto.BaseBranch;
        if (dto.BranchStrategy.HasValue) task.BranchStrategy = dto.BranchStrategy.Value;
        if (dto.MaxRuntimeSeconds.HasValue) task.MaxRuntimeSeconds = dto.MaxRuntimeSeconds.Value;
        if (dto.MaxFileChanges.HasValue) task.MaxFileChanges = dto.MaxFileChanges.Value;
        if (dto.IsEnabled.HasValue) task.IsEnabled = dto.IsEnabled.Value;
        if (dto.WorkflowDefinitionJson != null)
        {
            task.WorkflowDefinitionJson = dto.WorkflowDefinitionJson;
            task.WorkflowVersion++;
        }
        if (dto.DefaultInputsJson != null) task.DefaultInputsJson = dto.DefaultInputsJson;
        if (dto.RuntimeBackend != null) task.RuntimeBackend = dto.RuntimeBackend;
        if (dto.MaxConcurrentRuns.HasValue) task.MaxConcurrentRuns = dto.MaxConcurrentRuns.Value;
        if (dto.NodeFailurePolicyJson != null) task.NodeFailurePolicyJson = dto.NodeFailurePolicyJson;

        _logger.LogInformation("Updated task {TaskId}", id);
        return Task.FromResult(task);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task == null)
            {
                return Task.FromResult(false);
            }

            _tasks.Remove(task);
        }

        _logger.LogInformation("Deleted task {TaskId}", id);
        return Task.FromResult(true);
    }

    public Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == id);
            if (task != null)
            {
                task.LastRunAt = DateTime.UtcNow;
                task.LastStatus = status;
                task.LastError = error;
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateSchedulerStateAsync(Guid taskId, DateTime? nextRunAt, DateTime? lastQueuedAt, string schedulerStatus, DateTime? heartbeatAt)
    {
        lock (_lock)
        {
            var task = _tasks.FirstOrDefault(t => t.Id == taskId);
            if (task != null)
            {
                // null is meaningful: UnscheduleTaskAsync / SyncTaskAsync(disabled) pass null
                // to clear NextRunAt / LastQueuedAt so the task stops appearing in GetDueTasksAsync.
                task.NextRunAt = nextRunAt;
                task.LastQueuedAt = lastQueuedAt;
                task.SchedulerStatus = schedulerStatus;
                if (heartbeatAt.HasValue) task.SchedulerHeartbeatAt = heartbeatAt.Value;
            }
        }

        return Task.CompletedTask;
    }

    public Task<List<ScheduledTask>> GetDueTasksAsync(DateTime now)
    {
        lock (_lock)
        {
            return Task.FromResult(_tasks
                .Where(t => t.IsEnabled && t.NextRunAt != null && t.NextRunAt <= now)
                .ToList());
        }
    }
}
