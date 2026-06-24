using System.Text.Json;
using ChronoCode.Controllers;
using ChronoCode.Models;
using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using ChronoCode.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Additional AIController tests: success paths, delete/trigger, invalid action,
/// and AIStructuredResponse parsing edge cases.
/// </summary>
public class AIControllerAdditionalTests
{
    private static AIController CreateController(
        ITaskRepository? taskRepo = null,
        ISchedulerService? scheduler = null,
        RecordingChatRuntimeService? chatRuntime = null)
    {
        return new AIController(
            taskRepo ?? new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance),
            scheduler ?? new StubSchedulerService(),
            NullLogger<AIController>.Instance,
            chatRuntime ?? new RecordingChatRuntimeService(),
            new ChatMessageRequestValidator(),
            new CreateTaskDtoValidator(),
            new UpdateTaskDtoValidator());
    }

    private static async Task<(InMemoryTaskRepository repo, ScheduledTask task)> SeedTaskAsync()
    {
        var repo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await repo.CreateAsync(new CreateTaskDto
        {
            Name = "Existing Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(true, "Do work")
        });
        return (repo, task);
    }

    // ---- ExecuteStructuredResponse: create_task success ----

    [Fact]
    public async Task Execute_CreateTask_Valid_ReturnsOkWithTaskId()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = "New AI Task",
                Cron = "0 6 * * *",
                Repository = "https://github.com/test/repo",
                WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(false, "AI prompt")
            }
        });

        var okResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Execute_CreateTask_NullTask_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ---- ExecuteStructuredResponse: delete_task ----

    [Fact]
    public async Task Execute_DeleteTask_NoTaskId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Execute_DeleteTask_NotFound_Returns404()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Execute_DeleteTask_Existing_ReturnsNoContent()
    {
        var (repo, task) = await SeedTaskAsync();
        var controller = CreateController(repo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = task.Id
        });

        Assert.IsType<NoContentResult>(result);
        var deleted = await repo.GetByIdAsync(task.Id);
        Assert.Null(deleted);
    }

    // ---- ExecuteStructuredResponse: trigger_task ----

    [Fact]
    public async Task Execute_TriggerTask_NoTaskId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Execute_TriggerTask_NotFound_Returns404()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Execute_TriggerTask_Existing_ReturnsOk()
    {
        var (repo, task) = await SeedTaskAsync();
        var scheduler = new RecordingSchedulerService();
        var controller = CreateController(repo, scheduler);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = task.Id
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(task.Id, scheduler.LastTriggeredTaskId);
    }

    // ---- ExecuteStructuredResponse: update_task ----

    [Fact]
    public async Task Execute_UpdateTask_NoTaskId_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = null,
            Task = new AITaskDto { Name = "x", Cron = "0 0 * * *", Repository = "https://github.com/x/y" }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Execute_UpdateTask_NullTask_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = Guid.NewGuid(),
            Task = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Execute_UpdateTask_NotFound_Returns500()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = Guid.NewGuid(),
            Task = new AITaskDto
            {
                Name = "Updated",
                Cron = "0 0 * * *",
                Repository = "https://github.com/x/y",
                WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(false, "prompt")
            }
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task Execute_UpdateTask_Valid_ReturnsOk()
    {
        var (repo, task) = await SeedTaskAsync();
        var controller = CreateController(repo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = task.Id,
            Task = new AITaskDto
            {
                Name = "Updated Name",
                Cron = "0 12 * * *",
                Repository = "https://github.com/test/repo",
                WorkflowDefinitionJson = WorkflowDefinitionFactory.CreateDefaultJson(false, "updated prompt")
            }
        });

        Assert.IsType<OkObjectResult>(result);
        var updated = await repo.GetByIdAsync(task.Id);
        Assert.Equal("Updated Name", updated!.Name);
    }

    // ---- ExecuteStructuredResponse: invalid action ----

    [Fact]
    public async Task Execute_InvalidAction_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = "not_a_real_action"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ---- HandleChatMessage with create_task action response ----

    [Fact]
    public async Task HandleChatMessage_CreateTaskResponse_ReturnsStructuredResponse()
    {
        var chatRuntime = new RecordingChatRuntimeService();
        chatRuntime.Response = JsonSerializer.Serialize(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = "AI Task",
                Cron = "0 0 * * *",
                Repository = "https://github.com/test/repo"
            }
        });
        var controller = CreateController(chatRuntime: chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest
        {
            Message = "create a daily build task"
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AIStructuredResponse>(okResult.Value);
        Assert.Equal(AIActions.CreateTask, response.Action);
        Assert.NotNull(response.Task);
        Assert.Equal("AI Task", response.Task!.Name);
    }

    // ---- AIStructuredResponse parsing ----

    [Fact]
    public void AIStructuredResponse_Parse_InfoResponse()
    {
        var json = """{"action":"","task":null,"task_id":null,"error":{"code":"INFO","message":"help text"}}""";
        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);
        Assert.NotNull(response);
        Assert.Equal("", response!.Action);
        Assert.NotNull(response.Error);
        Assert.Equal("INFO", response.Error!.Code);
    }

    [Fact]
    public void AIStructuredResponse_Parse_CreateTask()
    {
        var json = """{"action":"create_task","task":{"name":"Build","cron":"0 0 * * *","repository":"https://github.com/x/y"},"task_id":null,"error":null}""";
        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);
        Assert.NotNull(response);
        Assert.Equal(AIActions.CreateTask, response!.Action);
        Assert.NotNull(response.Task);
        Assert.Equal("Build", response.Task!.Name);
    }

    [Fact]
    public void AIStructuredResponse_Parse_DeleteTask()
    {
        var json = """{"action":"delete_task","task":null,"task_id":"123e4567-e89b-12d3-a456-426614174000","error":null}""";
        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);
        Assert.NotNull(response);
        Assert.Equal(AIActions.DeleteTask, response!.Action);
        Assert.NotNull(response.TaskId);
    }

    [Fact]
    public void AIStructuredResponse_Parse_TriggerTask()
    {
        var json = """{"action":"trigger_task","task":null,"task_id":"123e4567-e89b-12d3-a456-426614174000","error":null}""";
        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);
        Assert.NotNull(response);
        Assert.Equal(AIActions.TriggerTask, response!.Action);
    }

    [Fact]
    public void AIStructuredResponse_Parse_EmptyAction()
    {
        var json = """{"action":"","task":null,"task_id":null,"error":null}""";
        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);
        Assert.NotNull(response);
        Assert.Equal("", response!.Action);
        Assert.Null(response.Task);
        Assert.Null(response.Error);
    }

    // ---- AIActions.IsValid ----

    [Theory]
    [InlineData(AIActions.CreateTask, true)]
    [InlineData(AIActions.UpdateTask, true)]
    [InlineData(AIActions.DeleteTask, true)]
    [InlineData(AIActions.TriggerTask, true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    public void AIActions_IsValid(string? action, bool expected)
    {
        Assert.Equal(expected, AIActions.IsValid(action));
    }

    // ---- Recording fakes ----

    private sealed class RecordingChatRuntimeService : IChatRuntimeService
    {
        public string? LastMessage { get; private set; }
        public string Response { get; set; } = "{}";
        public Exception? Exception { get; set; }

        public Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            if (Exception != null) throw Exception;
            return Task.FromResult(Response);
        }
    }

    private sealed class RecordingSchedulerService : ISchedulerService
    {
        public Guid? LastTriggeredTaskId;
        public Task SyncTaskAsync(ScheduledTask task, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken ct = default) { LastTriggeredTaskId = taskId; return Task.CompletedTask; }
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken ct = default) => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken ct = default) => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken ct = default) => Task.FromResult(new SchedulerQueueSnapshotDto());
    }

    private sealed class StubSchedulerService : ISchedulerService
    {
        public Task SyncTaskAsync(ScheduledTask task, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken ct = default) => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken ct = default) => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken ct = default) => Task.FromResult(new SchedulerQueueSnapshotDto());
    }
}
