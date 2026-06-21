using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TasksController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly IExecutionRepository _executionRepository;
    private readonly IAgentRuntime _agentRuntime;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        IExecutionRepository executionRepository,
        IAgentRuntime agentRuntime,
        ILogger<TasksController> logger)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _executionRepository = executionRepository;
        _agentRuntime = agentRuntime;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<TaskDto>> CreateTask([FromBody] CreateTaskDto dto)
    {
        var task = await _taskRepository.CreateAsync(dto);

        if (task.IsEnabled)
        {
            _schedulerService.ScheduleTask(task);
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
            return NotFound();

        return Ok(MapToDto(task));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TaskDto>> UpdateTask(Guid id, [FromBody] UpdateTaskDto dto)
    {
        try
        {
            var task = await _taskRepository.UpdateAsync(id, dto);

            _schedulerService.UnscheduleTask(id);
            if (task.IsEnabled)
            {
                _schedulerService.ScheduleTask(task);
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
        _schedulerService.UnscheduleTask(id);
        var result = await _taskRepository.DeleteAsync(id);

        if (!result)
            return NotFound();

        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> TriggerTask(Guid id)
    {
        var task = await _taskRepository.GetByIdAsync(id);
        if (task == null)
            return NotFound();

        _schedulerService.TriggerTask(id);
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

    [HttpGet("executions/{executionId:guid}/session")]
    public async Task<ActionResult<ExecutionSessionDto>> GetExecutionSession(Guid executionId)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return NotFound();
        }

        var liveSession = await _agentRuntime.GetExecutionSessionAsync(executionId);
        var backend = liveSession?.Backend ?? execution.AgentBackend ?? _agentRuntime.GetStatus().Backend;
        var supportsPersistentSessions = SupportsPersistentSessions(backend);
        var supportsSupplementalMessages = liveSession?.SupportsSupplementalMessages ?? SupportsSupplementalMessages(backend);
        var sessionId = liveSession?.SessionId ?? execution.AgentSessionId;
        var sessionFile = liveSession?.SessionFile ?? execution.AgentSessionFile;
        var workingDirectory = liveSession?.WorkingDirectory ?? execution.AgentWorkingDirectory;

        return Ok(new ExecutionSessionDto
        {
            ExecutionId = executionId,
            Backend = backend,
            SessionId = sessionId,
            SessionFile = sessionFile,
            WorkingDirectory = workingDirectory,
            IsLive = liveSession != null,
            SupportsPersistentSessions = supportsPersistentSessions,
            SupportsSupplementalMessages = supportsSupplementalMessages,
            CanResume = supportsPersistentSessions && (!string.IsNullOrWhiteSpace(sessionFile) || !string.IsNullOrWhiteSpace(sessionId))
        });
    }

    [HttpPost("executions/{executionId:guid}/resume")]
    public async Task<ActionResult<ExecutionSessionDto>> ResumeExecutionSession(Guid executionId, [FromBody] ResumeExecutionSessionDto? dto = null)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return NotFound();
        }

        var liveSession = await _agentRuntime.GetExecutionSessionAsync(executionId);
        if (liveSession != null)
        {
            return Ok(ToExecutionSessionDto(executionId, liveSession, isLive: true));
        }

        var status = _agentRuntime.GetStatus();
        var backend = execution.AgentBackend ?? status.Backend;
        if (!string.Equals(backend, status.Backend, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { Error = $"Execution was created with backend '{backend}', but current runtime is '{status.Backend}'." });
        }

        if (!SupportsPersistentSessions(backend))
        {
            return Conflict(new { Error = $"Backend '{backend}' does not support session resume." });
        }

        var sessionRef = string.IsNullOrWhiteSpace(dto?.SessionRef)
            ? execution.AgentSessionFile ?? execution.AgentSessionId
            : dto.SessionRef;

        if (string.IsNullOrWhiteSpace(sessionRef) || string.IsNullOrWhiteSpace(execution.AgentWorkingDirectory))
        {
            return Conflict(new { Error = "Execution does not have persisted session metadata to resume." });
        }

        var resumed = await _agentRuntime.ResumeExecutionSessionAsync(
            executionId,
            execution.AgentWorkingDirectory,
            sessionRef,
            chunk => _executionRepository.AddLogAsync(executionId, "Debug", chunk),
            HttpContext.RequestAborted);

        await _executionRepository.UpdateSessionAsync(executionId, resumed);
        await _executionRepository.AddLogAsync(executionId, "Info", "Resumed execution session", sessionRef);

        return Ok(ToExecutionSessionDto(executionId, resumed, isLive: true));
    }

    [HttpPost("executions/{executionId:guid}/message")]
    public async Task<IActionResult> SendExecutionMessage(Guid executionId, [FromBody] ExecutionMessageDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Message))
        {
            return BadRequest(new { Error = "Message is required." });
        }

        var execution = await _executionRepository.GetByIdAsync(executionId);
        if (execution == null)
        {
            return NotFound();
        }

        var session = await _agentRuntime.GetExecutionSessionAsync(executionId);
        if (session == null)
        {
            return Conflict(new { Error = "Execution has no live agent session." });
        }

        var mode = dto.Mode.Trim().ToLowerInvariant() switch
        {
            "prompt" => AgentMessageMode.Prompt,
            "follow_up" or "followup" => AgentMessageMode.FollowUp,
            _ => AgentMessageMode.Steer
        };

        var result = await _agentRuntime.SendMessageAsync(
            executionId,
            session.WorkingDirectory,
            dto.Message,
            mode,
            chunk => _executionRepository.AddLogAsync(executionId, "Debug", chunk),
            HttpContext.RequestAborted);

        await _executionRepository.AddLogAsync(executionId, "Info", $"Queued supplemental message ({mode})", dto.Message);

        return Ok(new
        {
            ExecutionId = executionId,
            Mode = mode.ToString(),
            Result = result,
            SessionId = session.SessionId,
            SessionFile = session.SessionFile
        });
    }

    [HttpGet("server/status")]
    public async Task<ActionResult> GetServerStatus()
    {
        var status = _agentRuntime.GetStatus();
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
            await _agentRuntime.EnsureReadyAsync();
            var status = _agentRuntime.GetStatus();
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
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("server/stop")]
    public async Task<IActionResult> StopServer()
    {
        await _agentRuntime.StopAsync();
        return Ok();
    }

    private static bool SupportsPersistentSessions(string? backend)
    {
        return string.Equals(backend, "pi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SupportsSupplementalMessages(string? backend)
    {
        return string.Equals(backend, "pi", StringComparison.OrdinalIgnoreCase);
    }

    private static ExecutionSessionDto ToExecutionSessionDto(Guid executionId, AgentExecutionSession session, bool isLive)
    {
        return new ExecutionSessionDto
        {
            ExecutionId = executionId,
            Backend = session.Backend,
            SessionId = session.SessionId,
            SessionFile = session.SessionFile,
            WorkingDirectory = session.WorkingDirectory,
            IsLive = isLive,
            SupportsPersistentSessions = SupportsPersistentSessions(session.Backend),
            SupportsSupplementalMessages = session.SupportsSupplementalMessages,
            CanResume = SupportsPersistentSessions(session.Backend) && (!string.IsNullOrWhiteSpace(session.SessionFile) || !string.IsNullOrWhiteSpace(session.SessionId))
        };
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
            Prompt = task.Prompt,
            MaxRuntimeSeconds = task.MaxRuntimeSeconds,
            MaxFileChanges = task.MaxFileChanges,
            RequirePlanReview = task.RequirePlanReview,
            CreatedAt = task.CreatedAt,
            LastRunAt = task.LastRunAt,
            LastStatus = task.LastStatus,
            IsEnabled = task.IsEnabled,
            LastError = task.LastError
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
            BranchName = execution.BranchName,
            CommitSha = execution.CommitSha,
            PrUrl = execution.PrUrl,
            FilesChanged = execution.FilesChanged,
            ErrorMessage = execution.ErrorMessage,
            AgentBackend = execution.AgentBackend,
            AgentSessionId = execution.AgentSessionId,
            AgentSessionFile = execution.AgentSessionFile,
            AgentWorkingDirectory = execution.AgentWorkingDirectory
        };
    }
}
