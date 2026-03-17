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
    private readonly IOpencodeServerManager _serverManager;
    private readonly ILogger<TasksController> _logger;

    public TasksController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        IExecutionRepository executionRepository,
        IOpencodeServerManager serverManager,
        ILogger<TasksController> logger)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _executionRepository = executionRepository;
        _serverManager = serverManager;
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

        return Ok(MapToDto(task));
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

    [HttpGet("server/status")]
    public async Task<ActionResult> GetServerStatus()
    {
        return Ok(new
        {
            Running = _serverManager.IsServerRunning,
            Url = _serverManager.ServerUrl
        });
    }

    [HttpPost("server/start")]
    public async Task<IActionResult> StartServer()
    {
        try
        {
            await _serverManager.StartServerAsync();
            await _serverManager.WaitForServerReadyAsync(TimeSpan.FromSeconds(30));
            return Ok(new { Url = _serverManager.ServerUrl });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("server/stop")]
    public async Task<IActionResult> StopServer()
    {
        await _serverManager.StopServerAsync();
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
            ErrorMessage = execution.ErrorMessage
        };
    }
}
