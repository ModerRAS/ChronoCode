using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/tasks")]
public class AIController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<AIController> _logger;

    public AIController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        ILogger<AIController> logger)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _logger = logger;
    }

    [HttpPost("ai")]
    public async Task<IActionResult> HandleAIStructuredResponse([FromBody] AIStructuredResponse response)
    {
        if (!AIActions.IsValid(response.Action))
        {
            return BadRequest(new { error = new { code = "INVALID_ACTION", message = $"Invalid action: {response.Action}" } });
        }

        switch (response.Action)
        {
            case AIActions.CreateTask:
                return await HandleCreateTask(response.Task);
            case AIActions.UpdateTask:
                return await HandleUpdateTask(response.TaskId, response.Task);
            case AIActions.DeleteTask:
                return await HandleDeleteTask(response.TaskId);
            case AIActions.TriggerTask:
                return await HandleTriggerTask(response.TaskId);
            default:
                return BadRequest(new { error = new { code = "INVALID_ACTION", message = "Unknown action" } });
        }
    }

    private async Task<IActionResult> HandleCreateTask(CreateTaskDto? dto)
    {
        if (dto == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Task data is required for create action" } });
        }

        var task = await _taskRepository.CreateAsync(dto);
        _logger.LogInformation("AI created task {TaskId}: {TaskName}", task.Id, task.Name);
        return CreatedAtAction(nameof(TasksController.GetTask), "Tasks", new { id = task.Id }, new { id = task.Id, name = task.Name });
    }

    private async Task<IActionResult> HandleUpdateTask(Guid? taskId, CreateTaskDto? dto)
    {
        if (taskId == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "TaskId is required for update action" } });
        }

        if (dto == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Task data is required for update action" } });
        }

        var updateDto = new UpdateTaskDto
        {
            Name = dto.Name,
            CronExpression = dto.CronExpression,
            RepositoryUrl = dto.RepositoryUrl,
            BaseBranch = dto.BaseBranch,
            BranchStrategy = dto.BranchStrategy,
            Prompt = dto.Prompt,
            MaxRuntimeSeconds = dto.MaxRuntimeSeconds,
            MaxFileChanges = dto.MaxFileChanges,
            RequirePlanReview = dto.RequirePlanReview,
            IsEnabled = dto.IsEnabled
        };

        var task = await _taskRepository.UpdateAsync(taskId.Value, updateDto);
        _logger.LogInformation("AI updated task {TaskId}", taskId);
        return Ok(new { id = task.Id, name = task.Name });
    }

    private async Task<IActionResult> HandleDeleteTask(Guid? taskId)
    {
        if (taskId == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "TaskId is required for delete action" } });
        }

        var deleted = await _taskRepository.DeleteAsync(taskId.Value);
        if (!deleted)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = $"Task {taskId} not found" } });
        }

        _logger.LogInformation("AI deleted task {TaskId}", taskId);
        return NoContent();
    }

    private async Task<IActionResult> HandleTriggerTask(Guid? taskId)
    {
        if (taskId == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "TaskId is required for trigger action" } });
        }

        var task = await _taskRepository.GetByIdAsync(taskId.Value);
        if (task == null)
        {
            return NotFound(new { error = new { code = "NOT_FOUND", message = $"Task {taskId} not found" } });
        }

        _schedulerService.TriggerTask(taskId.Value);
        _logger.LogInformation("AI triggered task {TaskId}", taskId);
        return Accepted(new { message = "Task triggered", taskId });
    }
}
