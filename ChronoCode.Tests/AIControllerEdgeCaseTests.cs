using ChronoCode.Controllers;
using ChronoCode.Models;
using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Additional AIController edge-case tests: invalid actions,
/// null task ID, delete not found, trigger not found, null task dto.
/// </summary>
public class AIControllerEdgeCaseTests
{
    [Fact]
    public async Task ExecuteStructuredResponse_InvalidAction_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = "invalid_action"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_EmptyAction_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = ""
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_NullAction_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = null!
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_CreateTask_NullTaskDto_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = null
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.NotNull(badRequest.Value);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_UpdateTask_NullTaskId_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = null,
            Task = new AITaskDto { Name = "Test", Cron = "0 0 * * *", Repository = "https://github.com/x/y" }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_UpdateTask_NullTaskDto_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = Guid.NewGuid(),
            Task = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_DeleteTask_NullTaskId_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_TriggerTask_NullTaskId_ReturnsBadRequest()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = null
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_DeleteTask_NotFound_ReturnsNotFound()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var controller = CreateController(new RecordingChatRuntimeService(), taskRepo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_TriggerTask_NotFound_ReturnsNotFound()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var controller = CreateController(new RecordingChatRuntimeService(), taskRepo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = Guid.NewGuid()
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_CreateTask_Valid_CreatesAndReturnsCreated()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var controller = CreateController(new RecordingChatRuntimeService(), taskRepo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = "AI Task",
                Cron = "0 9 * * *",
                Repository = "https://github.com/test/repo",
                IsEnabled = false
            }
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(created.Value);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_DeleteTask_Found_ReturnsNoContent()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await taskRepo.CreateAsync(new CreateTaskDto
        {
            Name = "Delete Me",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/x/y",
            WorkflowDefinitionJson = "{}"
        });
        var controller = CreateController(new RecordingChatRuntimeService(), taskRepo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = task.Id
        });

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_TriggerTask_Found_ReturnsOk()
    {
        var taskRepo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await taskRepo.CreateAsync(new CreateTaskDto
        {
            Name = "Trigger Me",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/x/y",
            WorkflowDefinitionJson = "{}",
            IsEnabled = true
        });
        var controller = CreateController(new RecordingChatRuntimeService(), taskRepo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = task.Id
        });

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task HandleChatMessage_WhitespaceMessage_ReturnsBadRequest()
    {
        var chatRuntime = new RecordingChatRuntimeService { Response = """{"error":{"code":"INFO","message":"hi"}}""" };
        var controller = CreateController(chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest { Message = "   " });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("", chatRuntime.LastMessage);
    }

    [Fact]
    public async Task HandleChatMessage_NullResponse_Returns500()
    {
        var chatRuntime = new RecordingChatRuntimeService { Response = null };
        var controller = CreateController(chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest { Message = "test" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task HandleChatMessage_GeneralException_Returns500()
    {
        var chatRuntime = new RecordingChatRuntimeService
        {
            Exception = new InvalidOperationException("unexpected error")
        };
        var controller = CreateController(chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest { Message = "test" });

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    private static AIController CreateController(
        RecordingChatRuntimeService chatRuntime,
        InMemoryTaskRepository? taskRepo = null)
    {
        taskRepo ??= new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var execRepo = new InMemoryExecutionRepository(NullLogger<InMemoryExecutionRepository>.Instance);
        var scheduler = new FakeSchedulerService();
        var createValidator = new CreateTaskDtoValidator();
        var updateValidator = new UpdateTaskDtoValidator();
        var chatValidator = new ChatMessageRequestValidator();

        return new AIController(
            taskRepo,
            scheduler,
            NullLogger<AIController>.Instance,
            chatRuntime,
            chatValidator,
            createValidator,
            updateValidator);
    }

    private sealed class RecordingChatRuntimeService : IChatRuntimeService
    {
        public string? Response { get; set; } = """{"error":{"code":"INFO","message":"ok"}}""";
        public Exception? Exception { get; set; }
        public string LastMessage { get; private set; } = "";

        public Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            if (Exception != null) throw Exception;
            return Task.FromResult(Response ?? throw new InvalidOperationException("null response"));
        }
    }

    private sealed class FakeSchedulerService : ISchedulerService
    {
        public Task SyncTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken cancellationToken = default) => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SchedulerQueueSnapshotDto());
    }
}
