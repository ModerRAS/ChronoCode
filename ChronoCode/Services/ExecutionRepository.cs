using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services;

public interface IExecutionRepository
{
    // Run-level
    Task<TaskExecution> CreateAsync(TaskExecution execution);
    Task<TaskExecution?> GetByIdAsync(Guid id);
    Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 20);
    Task UpdateAsync(TaskExecution execution);
    Task AddLogAsync(Guid executionId, string level, string message, string? details = null);
    Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId);
    Task<List<TaskExecution>> GetActiveRunsAsync();
    Task<int> CountActiveRunsAsync(Guid taskId);

    // Node-level
    Task<WorkflowNodeExecution> CreateNodeExecutionAsync(WorkflowNodeExecution node);
    Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid id);
    Task<List<WorkflowNodeExecution>> GetNodeExecutionsAsync(Guid executionId);
    Task<List<WorkflowNodeExecution>> GetRunningNodeExecutionsAsync();
    Task<List<WorkflowNodeExecution>> GetRetryableNodeExecutionsAsync(DateTime now);
    Task UpdateNodeExecutionAsync(WorkflowNodeExecution node);
    Task<WorkflowNodeExecution?> GetActiveNodeExecutionAsync(Guid executionId, string nodeId, string scopeKey);
    Task<WorkflowNodeExecution?> GetWaitingApprovalNodeAsync(Guid executionId, Guid nodeExecutionId);
}

public class InMemoryExecutionRepository : IExecutionRepository
{
    private readonly List<TaskExecution> _executions = new();
    private readonly List<WorkflowNodeExecution> _nodeExecutions = new();
    private readonly Dictionary<Guid, List<TaskLogEntry>> _logs = new();
    private readonly ILogger<InMemoryExecutionRepository> _logger;
    private readonly object _lock = new();

    public InMemoryExecutionRepository(ILogger<InMemoryExecutionRepository> logger)
    {
        _logger = logger;
    }

    public Task<TaskExecution> CreateAsync(TaskExecution execution)
    {
        lock (_lock)
        {
            _executions.Add(execution);
        }

        _logger.LogInformation("Created execution {ExecutionId} for task {TaskId}", execution.Id, execution.TaskId);
        return Task.FromResult(execution);
    }

    public Task<TaskExecution?> GetByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_executions.FirstOrDefault(e => e.Id == id));
        }
    }

    public Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 20)
    {
        lock (_lock)
        {
            return Task.FromResult(_executions
                .Where(e => e.TaskId == taskId)
                .OrderByDescending(e => e.StartedAt)
                .Take(limit)
                .ToList());
        }
    }

    public Task UpdateAsync(TaskExecution execution)
    {
        return Task.CompletedTask;
    }

    public Task AddLogAsync(Guid executionId, string level, string message, string? details = null)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(executionId, out var list))
            {
                list = new List<TaskLogEntry>();
                _logs[executionId] = list;
            }

            list.Add(new TaskLogEntry { Timestamp = DateTime.UtcNow, Level = level, Message = message, Details = details });
        }

        return Task.CompletedTask;
    }

    public Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId)
    {
        lock (_lock)
        {
            return Task.FromResult(_logs.TryGetValue(executionId, out var list)
                ? list.OrderBy(l => l.Timestamp).ToList()
                : new List<TaskLogEntry>());
        }
    }

    public Task<List<TaskExecution>> GetActiveRunsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_executions.Where(e => e.Status == Models.TaskStatus.Running).ToList());
        }
    }

    public Task<int> CountActiveRunsAsync(Guid taskId)
    {
        lock (_lock)
        {
            return Task.FromResult(_executions.Count(e => e.TaskId == taskId && e.Status == Models.TaskStatus.Running));
        }
    }

    public Task<WorkflowNodeExecution> CreateNodeExecutionAsync(WorkflowNodeExecution node)
    {
        lock (_lock)
        {
            _nodeExecutions.Add(node);
        }

        return Task.FromResult(node);
    }

    public Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_nodeExecutions.FirstOrDefault(n => n.Id == id));
        }
    }

    public Task<List<WorkflowNodeExecution>> GetNodeExecutionsAsync(Guid executionId)
    {
        lock (_lock)
        {
            return Task.FromResult(_nodeExecutions
                .Where(n => n.ExecutionId == executionId)
                .OrderBy(n => n.StartedAt)
                .ToList());
        }
    }

    public Task<List<WorkflowNodeExecution>> GetRunningNodeExecutionsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_nodeExecutions.Where(n => n.Status == WorkflowNodeStatus.Running).ToList());
        }
    }

    public Task<List<WorkflowNodeExecution>> GetRetryableNodeExecutionsAsync(DateTime now)
    {
        lock (_lock)
        {
            return Task.FromResult(_nodeExecutions
                .Where(n => n.Status == WorkflowNodeStatus.Retrying && n.NextRetryAt != null && n.NextRetryAt <= now)
                .ToList());
        }
    }

    public Task UpdateNodeExecutionAsync(WorkflowNodeExecution node)
    {
        return Task.CompletedTask;
    }

    public Task<WorkflowNodeExecution?> GetActiveNodeExecutionAsync(Guid executionId, string nodeId, string scopeKey)
    {
        lock (_lock)
        {
            var active = new[]
            {
                WorkflowNodeStatus.Pending,
                WorkflowNodeStatus.Running,
                WorkflowNodeStatus.Retrying,
                WorkflowNodeStatus.WaitingApproval,
                WorkflowNodeStatus.Completed
            };

            return Task.FromResult(_nodeExecutions
                .Where(n => n.ExecutionId == executionId && n.NodeId == nodeId && n.ScopeKey == scopeKey && active.Contains(n.Status))
                .OrderByDescending(n => n.StartedAt)
                .FirstOrDefault());
        }
    }

    public Task<WorkflowNodeExecution?> GetWaitingApprovalNodeAsync(Guid executionId, Guid nodeExecutionId)
    {
        lock (_lock)
        {
            return Task.FromResult(_nodeExecutions.FirstOrDefault(n =>
                n.Id == nodeExecutionId &&
                n.ExecutionId == executionId &&
                n.Status == WorkflowNodeStatus.WaitingApproval));
        }
    }
}
