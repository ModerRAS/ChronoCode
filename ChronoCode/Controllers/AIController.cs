using ChronoCode.Models;
using ChronoCode.Models.AI;
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
    private readonly IOpencodeClient _opencodeClient;
    private readonly IValidator<ChatMessageRequest> _chatMessageRequestValidator;
    private readonly IValidator<CreateTaskDto> _createTaskValidator;
    private readonly IValidator<UpdateTaskDto> _updateTaskValidator;

    public AIController(
        ITaskRepository taskRepository,
        ISchedulerService schedulerService,
        ILogger<AIController> logger,
        IOpencodeClient opencodeClient,
        IValidator<ChatMessageRequest> chatMessageRequestValidator,
        IValidator<CreateTaskDto> createTaskValidator,
        IValidator<UpdateTaskDto> updateTaskValidator)
    {
        _taskRepository = taskRepository;
        _schedulerService = schedulerService;
        _logger = logger;
        _opencodeClient = opencodeClient;
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

        var tempPath = Path.Combine(Path.GetTempPath(), "chronocode-chat", Guid.NewGuid().ToString());

        try
        {
            if (!_opencodeClient.IsServerAvailable())
            {
                return StatusCode(503, new { error = new { code = "SERVER_UNAVAILABLE", message = "AI server is not available. Please start the server first." } });
            }

            Directory.CreateDirectory(tempPath);

            var sessionId = await _opencodeClient.CreateSessionAsync(tempPath);
            
            var prompt = $@"You are a task management assistant. The user wants to manage scheduled tasks.

Available actions:
- create_task: Create a new scheduled task
- update_task: Update an existing task
- delete_task: Delete a task
- trigger_task: Manually trigger a task execution

User request: {request.Message}

Respond ONLY with a JSON object in this format:
{{
  ""action"": ""create_task|update_task|delete_task|trigger_task"",
  ""task_id"": ""uuid if updating/deleting/triggering, null otherwise"",
  ""task"": {{
    ""name"": ""task name"",
    ""cron"": ""cron expression (e.g., 0 2 * * *)"",
    ""repository"": ""https://github.com/owner/repo"",
    ""base_branch"": ""main"",
    ""prompt"": ""what the AI should do"",
    ""max_runtime_seconds"": 600,
    ""max_file_changes"": 50,
    ""require_plan_review"": true,
    ""is_enabled"": true
  }}
}}

If the user just wants information or help, respond with a JSON containing:
{{
  ""action"": """",
  ""task"": null,
  ""error"": {{ ""code"": ""INFO"", ""message"": ""your helpful response"" }}
}}";

            var response = await _opencodeClient.SendPromptAsync(sessionId, prompt, tempPath);
            
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
            _logger.LogError(ex, "Error handling chat message");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "An unexpected error occurred while processing your request." } });
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up AI temp directory {TempPath}", tempPath);
            }
        }
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

    private async Task<IActionResult> HandleCreateTask(AITaskDto? dto)
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
        _logger.LogInformation("AI created task {TaskId}: {TaskName}", task.Id, task.Name);
        return CreatedAtAction(nameof(TasksController.GetTask), "Tasks", new { id = task.Id }, new { id = task.Id, name = task.Name });
    }

    private async Task<IActionResult> HandleUpdateTask(Guid? taskId, AITaskDto? dto)
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
            Prompt = createDto.Prompt,
            MaxRuntimeSeconds = createDto.MaxRuntimeSeconds,
            MaxFileChanges = createDto.MaxFileChanges,
            RequirePlanReview = createDto.RequirePlanReview,
            IsEnabled = createDto.IsEnabled
        };

        var validationResult = await _updateTaskValidator.ValidateAsync(updateDto);
        if (!validationResult.IsValid)
        {
            return ValidationError(validationResult);
        }

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
