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

/// <summary>
/// AIController success-path tests: create_task, update_task, delete_task,
/// trigger_task with valid data. Also: chat message with create_task response.
/// </summary>
public class AIControllerSuccessTests
{
    [Fact]
    public async Task ExecuteStructuredResponse_CreateTask_Succeeds_WithValidData()
    {
        var repo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var controller = CreateController(repo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = "AI Task",
                Cron = "0 9 * * *",
                Repository = "https://github.com/owner/repo",
                RuntimeBackend = "pi"
            }
        });

        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.NotNull(createdResult.Value);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_UpdateTask_Succeeds_WithValidData()
    {
        var repo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var existing = await repo.CreateAsync(new CreateTaskDto
        {
            Name = "Original",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null)
        });

        var controller = CreateController(repo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = existing.Id,
            Task = new AITaskDto
            {
                Name = "Updated Name",
                Cron = "0 12 * * *",
                Repository = "https://github.com/owner/repo",
                RuntimeBackend = "pi"
            }
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_DeleteTask_Succeeds()
    {
        var repo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var existing = await repo.CreateAsync(new CreateTaskDto
        {
            Name = "To Delete",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null)
        });

        var controller = CreateController(repo);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.DeleteTask,
            TaskId = existing.Id
        });

        Assert.IsType<NoContentResult>(result);
        Assert.Null(await repo.GetByIdAsync(existing.Id));
    }

    [Fact]
    public async Task ExecuteStructuredResponse_TriggerTask_Succeeds()
    {
        var repo = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var existing = await repo.CreateAsync(new CreateTaskDto
        {
            Name = "To Trigger",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null)
        });

        var scheduler = new RecordingSchedulerService();
        var controller = CreateController(repo, scheduler);

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.TriggerTask,
            TaskId = existing.Id
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(existing.Id, scheduler.LastTriggeredTaskId);
    }

    [Fact]
    public async Task ExecuteStructuredResponse_DeleteTask_ReturnsNotFound_WhenTaskMissing()
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
    public async Task ExecuteStructuredResponse_TriggerTask_ReturnsNotFound_WhenTaskMissing()
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
    public async Task ExecuteStructuredResponse_UpdateTask_DoesNotCrash_WhenTaskMissing()
    {
        var controller = CreateController();

        var result = await controller.ExecuteStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = Guid.NewGuid(),
            Task = new AITaskDto
            {
                Name = "Valid",
                Cron = "0 0 * * *",
                Repository = "https://github.com/owner/repo"
            }
        });

        // Controller delegates to repo.UpdateAsync; verify it doesn't crash
        Assert.NotNull(result);
    }

    [Fact]
    public async Task HandleChatMessage_PassesMessageToRuntime()
    {
        var chatRuntime = new RecordingChatRuntimeService
        {
            Response = """{"action":"","task":null,"error":{"code":"INFO","message":"hi"}}"""
        };
        var controller = CreateController(chatRuntime: chatRuntime);

        await controller.HandleChatMessage(new ChatMessageRequest { Message = "hello" });

        Assert.Equal("hello", chatRuntime.LastMessage);
    }

    [Fact]
    public async Task HandleChatMessage_Returns500_OnUnexpectedException()
    {
        var chatRuntime = new RecordingChatRuntimeService
        {
            Exception = new InvalidOperationException("unexpected")
        };
        var controller = CreateController(chatRuntime: chatRuntime);

        var result = await controller.HandleChatMessage(new ChatMessageRequest { Message = "test" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    // ---- Helpers ----

    private static AIController CreateController(
        ITaskRepository? repo = null,
        ISchedulerService? scheduler = null,
        RecordingChatRuntimeService? chatRuntime = null)
    {
        return new AIController(
            repo ?? new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance),
            scheduler ?? new StubSchedulerService(),
            NullLogger<AIController>.Instance,
            chatRuntime ?? new RecordingChatRuntimeService(),
            new ChatMessageRequestValidator(),
            new CreateTaskDtoValidator(),
            new UpdateTaskDtoValidator());
    }

    private sealed class RecordingSchedulerService : ISchedulerService
    {
        public Guid? LastTriggeredTaskId { get; private set; }
        public ScheduledTask? LastSyncedTask { get; private set; }

        public Task SyncTaskAsync(ScheduledTask task, CancellationToken ct = default)
        { LastSyncedTask = task; return Task.CompletedTask; }
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken ct = default)
        { LastTriggeredTaskId = taskId; return Task.CompletedTask; }
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken ct = default)
            => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken ct = default)
            => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken ct = default)
            => Task.FromResult(new SchedulerQueueSnapshotDto());
    }

    private sealed class StubSchedulerService : ISchedulerService
    {
        public Task SyncTaskAsync(ScheduledTask task, CancellationToken ct = default) => Task.CompletedTask;
        public Task UnscheduleTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task TriggerTaskAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken ct = default)
            => Task.FromResult(new List<ScheduledTask>());
        public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken ct = default)
            => Task.FromResult(new List<DateTime>());
        public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken ct = default)
            => Task.FromResult(new SchedulerQueueSnapshotDto());
    }

    private sealed class RecordingChatRuntimeService : IChatRuntimeService
    {
        public string? LastMessage { get; private set; }
        public string Response { get; set; } = "{}";
        public Exception? Exception { get; set; }

        public Task<string> SendChatMessageAsync(string message, CancellationToken ct = default)
        {
            LastMessage = message;
            if (Exception != null) throw Exception;
            return Task.FromResult(Response);
        }
    }
}
