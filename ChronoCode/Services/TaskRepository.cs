using ChronoCode.Models;
using ChronoCode.Models.DTOs;

namespace ChronoCode.Services;

public interface ITaskRepository
{
    Task<ScheduledTask> CreateAsync(CreateTaskDto dto);
    Task<ScheduledTask?> GetByIdAsync(Guid id);
    Task<List<ScheduledTask>> GetAllAsync();
    Task<ScheduledTask> UpdateAsync(Guid id, UpdateTaskDto dto);
    Task<bool> DeleteAsync(Guid id);
    Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null);
}

public class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<ScheduledTask> _tasks = new();
    private readonly ILogger<InMemoryTaskRepository> _logger;

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
            Prompt = dto.Prompt,
            MaxRuntimeSeconds = dto.MaxRuntimeSeconds,
            MaxFileChanges = dto.MaxFileChanges,
            RequirePlanReview = dto.RequirePlanReview,
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            LastStatus = Models.TaskStatus.Pending
        };

        _tasks.Add(task);
        _logger.LogInformation("Created task {TaskId}: {TaskName}", task.Id, task.Name);
        return Task.FromResult(task);
    }

    public Task<ScheduledTask?> GetByIdAsync(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        return Task.FromResult(task);
    }

    public Task<List<ScheduledTask>> GetAllAsync()
    {
        return Task.FromResult(_tasks.ToList());
    }

    public Task<ScheduledTask> UpdateAsync(Guid id, UpdateTaskDto dto)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
            throw new KeyNotFoundException($"Task {id} not found");

        if (dto.Name != null) task.Name = dto.Name;
        if (dto.CronExpression != null) task.CronExpression = dto.CronExpression;
        if (dto.RepositoryUrl != null) task.RepositoryUrl = dto.RepositoryUrl;
        if (dto.BaseBranch != null) task.BaseBranch = dto.BaseBranch;
        if (dto.BranchStrategy.HasValue) task.BranchStrategy = dto.BranchStrategy.Value;
        if (dto.Prompt != null) task.Prompt = dto.Prompt;
        if (dto.MaxRuntimeSeconds.HasValue) task.MaxRuntimeSeconds = dto.MaxRuntimeSeconds.Value;
        if (dto.MaxFileChanges.HasValue) task.MaxFileChanges = dto.MaxFileChanges.Value;
        if (dto.RequirePlanReview.HasValue) task.RequirePlanReview = dto.RequirePlanReview.Value;
        if (dto.IsEnabled.HasValue) task.IsEnabled = dto.IsEnabled.Value;

        _logger.LogInformation("Updated task {TaskId}", id);
        return Task.FromResult(task);
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task == null)
            return Task.FromResult(false);

        _tasks.Remove(task);
        _logger.LogInformation("Deleted task {TaskId}", id);
        return Task.FromResult(true);
    }

    public Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null)
    {
        var task = _tasks.FirstOrDefault(t => t.Id == id);
        if (task != null)
        {
            task.LastRunAt = DateTime.UtcNow;
            task.LastStatus = status;
            task.LastError = error;
        }
        return Task.CompletedTask;
    }
}
