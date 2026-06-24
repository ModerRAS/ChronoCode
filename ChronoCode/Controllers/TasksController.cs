using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using ChronoCode.Models.Workflow;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly IExecutionRepository _executionRepository;
    private readonly IAgentRuntimeResolver _resolver;
    private readonly IWorkflowRunService _workflowRunService;
    private readonly IValidator<CreateTaskDto> _createTaskValidator;
    private readonly IValidator<UpdateTaskDto> _updateTaskValidator;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        IExecutionRepository executionRepository,
        IAgentRuntimeResolver resolver,
        IWorkflowRunService workflowRunService,
        IValidator<CreateTaskDto> createTaskValidator,
        IValidator<UpdateTaskDto> updateTaskValidator,
        ILogger<TasksController> logger)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _executionRepository = executionRepository;
        _resolver = resolver;
        _workflowRunService = workflowRunService;
        _createTaskValidator = createTaskValidator;
        _updateTaskValidator = updateTaskValidator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskDto dto)
    {
        var validation = await _createTaskValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
                }
            });
        }

        var task = await _taskRepository.CreateAsync(dto);

        if (task.IsEnabled)
        {
            await _schedulerService.SyncTaskAsync(task);
        }

        return CreatedAtAction(nameof(GetTask), new { id = task.Id }, MapToDto(task));
    }

    [HttpGet]
    public async Task<ActionResult<List<TaskDto>>> GetTasks()
    {
        var tasks = await _taskRepository.GetAllAsync();
        return Ok(tasks.Select(MapToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskDto>> GetTask(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskDto>> UpdateTask(Guid id, [FromBody] UpdateTaskDto dto)
    {
        var validation = await _updateTaskValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage))
                }
            });
        }

        try
        {
            var task = await _taskRepository.UpdateAsync(id, dto);
            if (task.IsEnabled)
            {
                await _schedulerService.SyncTaskAsync(task);
            }
            else
            {
                await _schedulerService.UnscheduleTaskAsync(id);
            }

            return Ok(MapToDto(task));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid id)
    {
        await _schedulerService.UnscheduleTaskAsync(id);
        var result = await _taskRepository.DeleteAsync(id);
        if (!result)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> TriggerTask(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
        {
            return NotFound();
        }

        await _schedulerService.TriggerTaskAsync(id);
        return Accepted();
    }

    [HttpGet("{id:guid}/executions")]
    public async Task<ActionResult<List<ExecutionDto>>> GetExecutions(Guid id)
    {
        var executions = await _executionRepository.GetByTaskIdAsync(id);
        return Ok(executions.Select(MapToExecutionDto).ToList());
    }

    [HttpGet("executions/{executionId:guid}/logs")]
    public async Task<ActionResult<List<LogDto>>> GetExecutionLogs(Guid executionId)
    {
        var logs = await _executionRepository.GetLogsAsync(executionId);
        return Ok(logs.Select(l => new LogDto
        {
            Timestamp = l.Timestamp,
            Level = l.Level,
            Message = l.Message,
            Details = l.Details
        }).ToList());
    }

    [HttpGet("executions/{executionId:guid}/nodes")]
    public async Task<ActionResult<List<NodeExecutionDto>>> GetNodeExecutions(Guid executionId)
    {
        var nodes = await _executionRepository.GetNodeExecutionsAsync(executionId);
        return Ok(nodes.Select(MapToNodeExecutionDto).ToList());
    }

    [HttpGet("executions/{executionId:guid}/nodes/{nodeExecutionId:guid}/session")]
    public async Task<ActionResult<ExecutionSessionDto>> GetNodeSession(Guid executionId, Guid nodeExecutionId)
    {
        var nodeExec = await _workflowRunService.GetNodeExecutionAsync(executionId, nodeExecutionId);
        if (nodeExec == null)
        {
            return NotFound();
        }

        var backend = nodeExec.AgentBackend ?? _resolver.GetStatus(null).Backend;
        var supportsPersistentSessions = string.Equals(backend, "pi", StringComparison.OrdinalIgnoreCase);
        var supportsSupplementalMessages = string.Equals(backend, "pi", StringComparison.OrdinalIgnoreCase);

        return Ok(new ExecutionSessionDto
        {
            ExecutionId = executionId,
            NodeExecutionId = nodeExecutionId,
            Backend = backend,
            SessionId = nodeExec.AgentSessionId,
            SessionFile = nodeExec.AgentSessionFile,
            WorkingDirectory = nodeExec.AgentWorkingDirectory,
            IsLive = false,
            SupportsPersistentSessions = supportsPersistentSessions,
            SupportsSupplementalMessages = supportsSupplementalMessages,
            CanResume = supportsPersistentSessions
                && (!string.IsNullOrWhiteSpace(nodeExec.AgentSessionFile) || !string.IsNullOrWhiteSpace(nodeExec.AgentSessionId))
        });
    }

    [HttpPost("executions/{executionId:guid}/nodes/{nodeExecutionId:guid}/resume")]
    public async Task<ActionResult<ExecutionSessionDto>> ResumeNodeSession(Guid executionId, Guid nodeExecutionId, [FromBody] ResumeExecutionSessionDto? dto = null)
    {
        var nodeExec = await _workflowRunService.GetNodeExecutionAsync(executionId, nodeExecutionId);
        if (nodeExec == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(nodeExec.AgentSessionFile) && string.IsNullOrWhiteSpace(nodeExec.AgentSessionId))
        {
            return Conflict(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "Node execution does not have persisted session metadata to resume."
                }
            });
        }

        var resumed = await _workflowRunService.ResumeNodeSessionAsync(executionId, nodeExecutionId, dto?.SessionRef, HttpContext.RequestAborted);
        return Ok(new ExecutionSessionDto
        {
            ExecutionId = executionId,
            NodeExecutionId = nodeExecutionId,
            Backend = resumed.Backend,
            SessionId = resumed.SessionId,
            SessionFile = resumed.SessionFile,
            WorkingDirectory = resumed.WorkingDirectory,
            IsLive = true,
            SupportsPersistentSessions = string.Equals(resumed.Backend, "pi", StringComparison.OrdinalIgnoreCase),
            SupportsSupplementalMessages = resumed.SupportsSupplementalMessages,
            CanResume = true
        });
    }

    [HttpPost("executions/{executionId:guid}/nodes/{nodeExecutionId:guid}/message")]
    public async Task<IActionResult> SendNodeMessage(Guid executionId, Guid nodeExecutionId, [FromBody] ExecutionMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "Message is required."
                }
            });
        }

        var nodeExec = await _workflowRunService.GetNodeExecutionAsync(executionId, nodeExecutionId);
        if (nodeExec == null)
        {
            return NotFound();
        }

        var mode = dto.Mode.Trim().ToLowerInvariant() switch
        {
            "prompt" => AgentMessageMode.Prompt,
            "follow_up" or "followup" => AgentMessageMode.FollowUp,
            _ => AgentMessageMode.Steer
        };

        var result = await _workflowRunService.SendNodeMessageAsync(
            executionId,
            nodeExecutionId,
            dto.Message,
            mode.ToString(),
            HttpContext.RequestAborted);

        return Ok(new
        {
            ExecutionId = executionId,
            NodeExecutionId = nodeExecutionId,
            Mode = mode.ToString(),
            Result = result
        });
    }

    [HttpPost("executions/{executionId:guid}/approval/{nodeExecutionId:guid}")]
    public async Task<IActionResult> ApproveNode(Guid executionId, Guid nodeExecutionId, [FromBody] ApprovalRequestDto dto)
    {
        await _workflowRunService.ApproveNodeAsync(executionId, nodeExecutionId, dto.Approved, dto.Reason, HttpContext.RequestAborted);
        return Ok();
    }

    [HttpGet("server/status")]
    public ActionResult GetServerStatus()
    {
        var status = _resolver.GetStatus(null);
        return Ok(new
        {
            Backend = status.Backend,
            Running = status.IsReady,
            Url = status.Endpoint,
            SupportsPersistentSessions = status.SupportsPersistentSessions,
            SupportsSupplementalMessages = status.SupportsSupplementalMessages
        });
    }

    [HttpPost("server/start")]
    public async Task<IActionResult> StartServer()
    {
        try
        {
            await _resolver.Get(null).EnsureReadyAsync();
            var status = _resolver.GetStatus(null);
            return Ok(new
            {
                Backend = status.Backend,
                Url = status.Endpoint,
                SupportsPersistentSessions = status.SupportsPersistentSessions,
                SupportsSupplementalMessages = status.SupportsSupplementalMessages
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = ex.Message } });
        }
    }

    [HttpPost("server/stop")]
    public async Task<IActionResult> StopServer()
    {
        await _resolver.Get(null).StopAsync();
        return Ok();
    }

    private static TaskDto MapToDto(ScheduledTask task)
    {
        return new TaskDto
        {
            Id = task.Id,
            Name = task.Name,
            CronExpression = task.CronExpression,
            RepositoryUrl = task.RepositoryUrl,
            BaseBranch = task.BaseBranch,
            BranchStrategy = task.BranchStrategy,
            MaxRuntimeSeconds = task.MaxRuntimeSeconds,
            MaxFileChanges = task.MaxFileChanges,
            IsEnabled = task.IsEnabled,
            WorkflowVersion = task.WorkflowVersion,
            WorkflowDefinitionJson = task.WorkflowDefinitionJson,
            DefaultInputsJson = task.DefaultInputsJson,
            RuntimeBackend = task.RuntimeBackend,
            MaxConcurrentRuns = task.MaxConcurrentRuns,
            NodeFailurePolicyJson = task.NodeFailurePolicyJson,
            CreatedAt = task.CreatedAt,
            LastRunAt = task.LastRunAt,
            LastStatus = task.LastStatus,
            LastError = task.LastError,
            NextRunAt = task.NextRunAt,
            LastQueuedAt = task.LastQueuedAt,
            SchedulerStatus = task.SchedulerStatus,
            SchedulerHeartbeatAt = task.SchedulerHeartbeatAt
        };
    }

    private static ExecutionDto MapToExecutionDto(TaskExecution execution)
    {
        return new ExecutionDto
        {
            Id = execution.Id,
            TaskId = execution.TaskId,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            Status = execution.Status,
            WorkflowVersion = execution.WorkflowVersion,
            CurrentNodeId = execution.CurrentNodeId,
            TriggerSource = execution.TriggerSource,
            BranchName = execution.BranchName,
            CommitSha = execution.CommitSha,
            PrUrl = execution.PrUrl,
            FilesChanged = execution.FilesChanged,
            ErrorMessage = execution.ErrorMessage
        };
    }

    private static NodeExecutionDto MapToNodeExecutionDto(WorkflowNodeExecution node)
    {
        return new NodeExecutionDto
        {
            Id = node.Id,
            ExecutionId = node.ExecutionId,
            NodeId = node.NodeId,
            NodeType = node.NodeType,
            ScopeKey = node.ScopeKey,
            Attempt = node.Attempt,
            Status = node.Status,
            StartedAt = node.StartedAt,
            CompletedAt = node.CompletedAt,
            OutputJson = node.OutputJson,
            ValidationError = node.ValidationError,
            AgentBackend = node.AgentBackend,
            AgentSessionId = node.AgentSessionId,
            AgentSessionFile = node.AgentSessionFile,
            AgentWorkingDirectory = node.AgentWorkingDirectory,
            FailureReason = node.FailureReason,
            NextRetryAt = node.NextRetryAt,
            RetryCount = node.RetryCount,
            LeaseExpiresAt = node.LeaseExpiresAt
        };
    }
}
