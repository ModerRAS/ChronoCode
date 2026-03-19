using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
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
            Prompt = dto.Prompt,
            MaxRuntimeSeconds = dto.MaxRuntimeSeconds,
            MaxFileChanges = dto.MaxFileChanges,
            RequirePlanReview = dto.RequirePlanReview,
            IsEnabled = dto.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            LastStatus = Models.TaskStatus.Pending
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
        var task = await _context.ScheduledTasks.FindAsync(id);
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

        await _context.SaveChangesAsync();
        _logger.LogInformation("Updated task {TaskId}", id);
        return task;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var task = await _context.ScheduledTasks.FindAsync(id);
        if (task == null)
            return false;

        _context.ScheduledTasks.Remove(task);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Deleted task {TaskId}", id);
        return true;
    }

    public async Task UpdateLastRunAsync(Guid id, Models.TaskStatus status, string? error = null)
    {
        var task = await _context.ScheduledTasks.FindAsync(id);
        if (task != null)
        {
            task.LastRunAt = DateTime.UtcNow;
            task.LastStatus = status;
            task.LastError = error;
            await _context.SaveChangesAsync();
        }
    }
}
