using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace ChronoCode.Controllers;

[ApiController]
[Route("api/ai")]
public class AIController : ControllerBase
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISchedulerService _schedulerService;
    private readonly ILogger<AIController> _logger;
    private readonly IChatRuntimeService _chatRuntimeService;
    private readonly IValidator<ChatMessageRequest> _chatMessageRequestValidator;
    private readonly IValidator<CreateTaskDto> _createTaskValidator;
    private readonly IValidator<UpdateTaskDto> _updateTaskValidator;

    public AIController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        ILogger<AIController> logger,
        IChatRuntimeService chatRuntimeService,
        IValidator<ChatMessageRequest> chatMessageRequestValidator,
        IValidator<CreateTaskDto> createTaskValidator,
        IValidator<UpdateTaskDto> updateTaskValidator)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _logger = logger;
        _chatRuntimeService = chatRuntimeService;
        _chatMessageRequestValidator = chatMessageRequestValidator;
        _createTaskValidator = createTaskValidator;
        _updateTaskValidator = updateTaskValidator;
    }

    [HttpPost("message")]
    public async Task<IActionResult> HandleChatMessage([FromBody] ChatMessageRequest request)
    {
        var requestValidation = await _chatMessageRequestValidator.ValidateAsync(request);
        if (!requestValidation.IsValid)
        {
            return ValidationError(requestValidation);
        }

        try
        {
            var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
            var response = await _chatRuntimeService.SendChatMessageAsync(request.Message, request.History, cancellationToken);

            try
            {
                var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"```json\s*([\s\S]*?)\s*```|$");
                var jsonStr = jsonMatch.Success && jsonMatch.Value.StartsWith("```")
                    ? jsonMatch.Groups[1].Value
                    : response;

                var structuredResponse = System.Text.Json.JsonSerializer.Deserialize<Models.AI.AIStructuredResponse>(jsonStr);
                if (structuredResponse != null)
                {
                    return Ok(structuredResponse);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse AI JSON response. Payload: {Response}", response);
            }

            return Ok(new Models.AI.AIStructuredResponse
            {
                Error = new Models.AI.AIError
                {
                    Code = "INFO",
                    Message = response
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling AI chat message");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = ex.Message } });
        }
    }

[HttpPost("ai")]
    public async Task<IActionResult> ExecuteStructuredResponse([FromBody] Models.AI.AIStructuredResponse response)
    {
        if (!Models.AI.AIActions.IsValid(response.Action))
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Invalid AI action" } });
        }

        try
        {
            return response.Action switch
            {
                Models.AI.AIActions.CreateTask => await HandleCreateTask(response.Task),
                Models.AI.AIActions.UpdateTask => await HandleUpdateTask(response.TaskId, response.Task),
                Models.AI.AIActions.DeleteTask => await HandleDeleteTask(response.TaskId),
                Models.AI.AIActions.TriggerTask => await HandleTriggerTask(response.TaskId),
                _ => BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Unsupported AI action" } })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing AI structured response");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = ex.Message } });
        }
    }

    private async Task<IActionResult> HandleCreateTask(Models.AI.AITaskDto? dto)
    {
        if (dto == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Task data is required for create action" } });
        }

        var createDto = dto.ToCreateTaskDto();
        var validationResult = await _createTaskValidator.ValidateAsync(createDto);
        if (!validationResult.IsValid)
        {
            return ValidationError(validationResult);
        }

        var task = await _taskRepository.CreateAsync(createDto);
        if (task.IsEnabled)
        {
            await _schedulerService.SyncTaskAsync(task);
        }

        _logger.LogInformation("AI created task {TaskId}: {TaskName}", task.Id, task.Name);
        return CreatedAtAction(nameof(TasksController.GetTask), "Tasks", new { id = task.Id }, new { id = task.Id, name = task.Name });
    }

    private async Task<IActionResult> HandleUpdateTask(Guid? taskId, Models.AI.AITaskDto? dto)
    {
        if (taskId == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "TaskId is required for update action" } });
        }

        if (dto == null)
        {
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Task data is required for update action" } });
        }

        var createDto = dto.ToCreateTaskDto();
        var updateDto = new UpdateTaskDto
        {
            Name = createDto.Name,
            CronExpression = createDto.CronExpression,
            RepositoryUrl = createDto.RepositoryUrl,
            BaseBranch = createDto.BaseBranch,
            BranchStrategy = createDto.BranchStrategy,
            MaxRuntimeSeconds = createDto.MaxRuntimeSeconds,
            MaxFileChanges = createDto.MaxFileChanges,
            IsEnabled = createDto.IsEnabled,
            WorkflowDefinitionJson = createDto.WorkflowDefinitionJson,
            DefaultInputsJson = createDto.DefaultInputsJson,
            RuntimeBackend = createDto.RuntimeBackend,
            MaxConcurrentRuns = createDto.MaxConcurrentRuns,
            NodeFailurePolicyJson = createDto.NodeFailurePolicyJson
        };

        var validationResult = await _updateTaskValidator.ValidateAsync(updateDto);
        if (!validationResult.IsValid)
        {
            return ValidationError(validationResult);
        }

        var task = await _taskRepository.UpdateAsync(taskId.Value, updateDto);
        if (task.IsEnabled)
        {
            await _schedulerService.SyncTaskAsync(task);
        }
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

        await _schedulerService.UnscheduleTaskAsync(taskId.Value);
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
            return NotFound(new { error = new { code = "NOT_FOUND", message = "Task not found" } });
        }

        await _schedulerService.TriggerTaskAsync(taskId.Value);
        _logger.LogInformation("AI triggered task {TaskId}", taskId);
        return Ok(new { id = taskId.Value });
    }

    private BadRequestObjectResult ValidationError(ValidationResult validationResult)
    {
        return BadRequest(new
        {
            error = new
            {
                code = "VALIDATION_ERROR",
                message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage))
            }
        });
    }
}
