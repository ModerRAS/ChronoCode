using System.Text.Json;
using ChronoCode.Data;
using ChronoCode.Models;
using Microsoft.EntityFrameworkCore;

namespace ChronoCode.Services;

public class EfExecutionRepository : IExecutionRepository
{
    private readonly ChronoDbContext _context;
    private readonly ILogger<EfExecutionRepository> _logger;

    public EfExecutionRepository(ChronoDbContext context, ILogger<EfExecutionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TaskExecution> CreateAsync(Guid taskId)
    {
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
            Status = Models.TaskStatus.Running,
            Logs = new List<string>()
        };

        _context.TaskExecutions.Add(execution);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created execution {ExecutionId} for task {TaskId}", execution.Id, taskId);
        return execution;
    }

    public async Task<TaskExecution?> GetByIdAsync(Guid id)
    {
        return await _context.TaskExecutions.FindAsync(id);
    }

    public async Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 10)
    {
        return await _context.TaskExecutions
            .Where(e => e.TaskId == taskId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task UpdateAsync(TaskExecution execution)
    {
        _context.TaskExecutions.Update(execution);
        await _context.SaveChangesAsync();
    }

    public async Task AddLogAsync(Guid executionId, string level, string message, string? details = null)
    {
        var execution = await _context.TaskExecutions.FindAsync(executionId);
        if (execution != null)
        {
            var logEntry = new TaskLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Details = details
            };
            execution.Logs.Add(JsonSerializer.Serialize(logEntry));
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId)
    {
        var execution = await _context.TaskExecutions.FindAsync(executionId);
        if (execution == null)
            return new List<TaskLogEntry>();

        return execution.Logs
            .Select(log => JsonSerializer.Deserialize<TaskLogEntry>(log) ?? new TaskLogEntry { Message = log })
            .ToList();
    }
}
