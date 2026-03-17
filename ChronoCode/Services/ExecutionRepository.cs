using ChronoCode.Models;

namespace ChronoCode.Services;

public interface IExecutionRepository
{
    Task<TaskExecution> CreateAsync(Guid taskId);
    Task<TaskExecution?> GetByIdAsync(Guid id);
    Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 10);
    Task UpdateAsync(TaskExecution execution);
    Task AddLogAsync(Guid executionId, string level, string message, string? details = null);
    Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId);
}

public class InMemoryExecutionRepository : IExecutionRepository
{
    private readonly List<TaskExecution> _executions = new();
    private readonly Dictionary<Guid, List<TaskLogEntry>> _logs = new();
    private readonly ILogger<InMemoryExecutionRepository> _logger;

    public InMemoryExecutionRepository(ILogger<InMemoryExecutionRepository> logger)
    {
        _logger = logger;
    }

    public Task<TaskExecution> CreateAsync(Guid taskId)
    {
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            StartedAt = DateTime.UtcNow,
            Status = Models.TaskStatus.Running
        };

        _executions.Add(execution);
        _logs[execution.Id] = new List<TaskLogEntry>();
        _logger.LogInformation("Created execution {ExecutionId} for task {TaskId}", execution.Id, taskId);
        return Task.FromResult(execution);
    }

    public Task<TaskExecution?> GetByIdAsync(Guid id)
    {
        var execution = _executions.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(execution);
    }

    public Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 10)
    {
        var executions = _executions
            .Where(e => e.TaskId == taskId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(executions);
    }

    public Task UpdateAsync(TaskExecution execution)
    {
        var index = _executions.FindIndex(e => e.Id == execution.Id);
        if (index >= 0)
        {
            _executions[index] = execution;
        }
        return Task.CompletedTask;
    }

    public Task AddLogAsync(Guid executionId, string level, string message, string? details = null)
    {
        if (_logs.TryGetValue(executionId, out var logEntries))
        {
            logEntries.Add(new TaskLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Details = details
            });
        }
        return Task.CompletedTask;
    }

    public Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId)
    {
        var logs = _logs.GetValueOrDefault(executionId, new List<TaskLogEntry>());
        return Task.FromResult(logs.ToList());
    }
}
