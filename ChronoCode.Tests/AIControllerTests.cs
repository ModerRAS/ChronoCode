using System.Text.Json;
using ChronoCode.Controllers;
using ChronoCode.Models;
using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using ChronoCode.Validators;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChronoCode.Tests;

public class AIControllerTests
{
    [Fact]
    public async Task HandleChatMessage_ReturnsStructuredInfoResponse()
    {
        var chatRuntime = new RecordingChatRuntimeService
        {
            Response = """
            {"error":{"code":"INFO","message":"ok"}}
            """
        };
        var controller = CreateController(chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest
        {
            Message = "help me summarize tasks"
        });

        var objectResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AIStructuredResponse>(objectResult.Value);
        Assert.Equal(string.Empty, response.Action);
        Assert.Null(response.TaskId);
        Assert.Null(response.Task);
        Assert.NotNull(response.Error);
        Assert.Equal("INFO", response.Error.Code);
        Assert.Equal("ok", response.Error.Message);
        Assert.Equal("help me summarize tasks", chatRuntime.LastMessage);
    }

    [Fact]
    public async Task HandleChatMessage_ReturnsServerUnavailable_WhenRuntimeUnavailable()
    {
        var chatRuntime = new RecordingChatRuntimeService
        {
            Exception = new HttpRequestException("runtime unavailable")
        };
        var controller = CreateController(chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest
        {
            Message = "create a task"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_CreateTask_ReturnsBadRequest_WhenTaskIsInvalid()
    {
        var controller = CreateController(new RecordingChatRuntimeService());

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = string.Empty,
                Cron = "bad-cron",
                Repository = "not-a-url"
            }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_UpdateTask_ReturnsBadRequest_WhenTaskIsInvalid()
    {
        var repository = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await repository.CreateAsync(new Models.DTOs.CreateTaskDto
        {
            Name = "Existing task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Do work")
        });
        var controller = CreateController(new RecordingChatRuntimeService(), repository);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = task.Id,
            Task = new AITaskDto
            {
                Name = string.Empty,
                Cron = "bad-cron",
                Repository = "still-not-a-url"
            }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AIController CreateController(RecordingChatRuntimeService chatRuntimeService, ITaskRepository? taskRepository = null)
    {
        return new AIController(
            taskRepository ?? new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance),
            new StubSchedulerService(),
            NullLogger<AIController>.Instance,
            chatRuntimeService,
            new ChatMessageRequestValidator(),
            new CreateTaskDtoValidator(),
            new UpdateTaskDtoValidator());
    }

    private sealed class RecordingChatRuntimeService : IChatRuntimeService
    {
        public string? LastMessage { get; private set; }
        public string Response { get; set; } = "{}";
        public Exception? Exception { get; set; }

        public Task<string> SendChatMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            LastMessage = message;
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class StubSchedulerService : ISchedulerService
    {
        public Task SyncTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken cancellationToken = default) => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SchedulerQueueSnapshotDto());
    }
}
