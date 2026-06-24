using System.Text.Json;
using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
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

    public async Task<TaskExecution> CreateAsync(TaskExecution execution)
    {
        execution.Logs ??= new List<string>();
        _context.TaskExecutions.Add(execution);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Created execution {ExecutionId} for task {TaskId}", execution.Id, execution.TaskId);
        return execution;
    }

    public async Task<TaskExecution?> GetByIdAsync(Guid id)
    {
        return await _context.TaskExecutions.FindAsync(id);
    }

    public async Task<List<TaskExecution>> GetByTaskIdAsync(Guid taskId, int limit = 20)
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
        if (execution == null)
        {
            return;
        }

        var logEntry = new TaskLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Level = level,
            Message = message,
            Details = details
        };

        execution.Logs ??= new List<string>();
        execution.Logs.Add(JsonSerializer.Serialize(logEntry));
        await _context.SaveChangesAsync();
    }

    public async Task<List<TaskLogEntry>> GetLogsAsync(Guid executionId)
    {
        var execution = await _context.TaskExecutions.FindAsync(executionId);
        if (execution == null)
        {
            return new List<TaskLogEntry>();
        }

        return execution.Logs
            .Select(log => JsonSerializer.Deserialize<TaskLogEntry>(log) ?? new TaskLogEntry { Message = log })
            .ToList();
    }

    public async Task<List<TaskExecution>> GetActiveRunsAsync()
    {
        return await _context.TaskExecutions
            .Where(e => e.Status == Models.TaskStatus.Running)
            .ToListAsync();
    }

    public async Task<int> CountActiveRunsAsync(Guid taskId)
    {
        return await _context.TaskExecutions
            .CountAsync(e => e.TaskId == taskId && e.Status == Models.TaskStatus.Running);
    }

    public async Task<WorkflowNodeExecution> CreateNodeExecutionAsync(WorkflowNodeExecution node)
    {
        _context.WorkflowNodeExecutions.Add(node);
        await _context.SaveChangesAsync();
        return node;
    }

    public async Task<WorkflowNodeExecution?> GetNodeExecutionAsync(Guid id)
    {
        return await _context.WorkflowNodeExecutions.FindAsync(id);
    }

    public async Task<List<WorkflowNodeExecution>> GetNodeExecutionsAsync(Guid executionId)
    {
        return await _context.WorkflowNodeExecutions
            .Where(n => n.ExecutionId == executionId)
            .OrderBy(n => n.StartedAt)
            .ToListAsync();
    }

    public async Task<List<WorkflowNodeExecution>> GetRunningNodeExecutionsAsync()
    {
        return await _context.WorkflowNodeExecutions
            .Where(n => n.Status == WorkflowNodeStatus.Running)
            .ToListAsync();
    }

    public async Task<List<WorkflowNodeExecution>> GetRetryableNodeExecutionsAsync(DateTime now)
    {
        return await _context.WorkflowNodeExecutions
            .Where(n => n.Status == WorkflowNodeStatus.Retrying && n.NextRetryAt != null && n.NextRetryAt <= now)
            .ToListAsync();
    }

    public async Task UpdateNodeExecutionAsync(WorkflowNodeExecution node)
    {
        _context.WorkflowNodeExecutions.Update(node);
        await _context.SaveChangesAsync();
    }

    public async Task<WorkflowNodeExecution?> GetActiveNodeExecutionAsync(Guid executionId, string nodeId, string scopeKey)
    {
        var activeStatuses = new[]
        {
            WorkflowNodeStatus.Pending,
            WorkflowNodeStatus.Running,
            WorkflowNodeStatus.Retrying,
            WorkflowNodeStatus.WaitingApproval,
            WorkflowNodeStatus.Completed
        };

        return await _context.WorkflowNodeExecutions
            .Where(n => n.ExecutionId == executionId
                        && n.NodeId == nodeId
                        && n.ScopeKey == scopeKey
                        && activeStatuses.Contains(n.Status))
            .OrderByDescending(n => n.StartedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<WorkflowNodeExecution?> GetWaitingApprovalNodeAsync(Guid executionId, Guid nodeExecutionId)
    {
        return await _context.WorkflowNodeExecutions
            .Where(n => n.Id == nodeExecutionId
                        && n.ExecutionId == executionId
                        && n.Status == WorkflowNodeStatus.WaitingApproval)
            .FirstOrDefaultAsync();
    }
}
