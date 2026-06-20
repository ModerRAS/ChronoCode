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
    public async Task HandleChatMessage_DeletesTemporaryDirectory_AfterSuccessfulRequest()
    {
        var opencodeClient = new RecordingOpencodeClient
        {
            SendPromptResult = JsonSerializer.Serialize(new AIStructuredResponse
            {
                Error = new AIError
                {
                    Code = "INFO",
                    Message = "ok"
                }
            })
        };
        var controller = CreateController(opencodeClient);

        var result = await controller.HandleChatMessage(new ChatMessageRequest
        {
            Message = "help me summarize tasks"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(opencodeClient.WorkingDirectory);
        Assert.False(Directory.Exists(opencodeClient.WorkingDirectory));
    }

    [Fact]
    public async Task HandleChatMessage_ReturnsGenericInternalErrorMessage_WhenClientThrows()
    {
        var opencodeClient = new RecordingOpencodeClient
        {
            CreateSessionException = new InvalidOperationException("secret filesystem path")
        };
        var controller = CreateController(opencodeClient);

        var result = await controller.HandleChatMessage(new ChatMessageRequest
        {
            Message = "trigger an error"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        Assert.DoesNotContain("secret filesystem path", JsonSerializer.Serialize(objectResult.Value));
    }

    [Fact]
    public async Task HandleAIStructuredResponse_CreateTask_ReturnsBadRequest_WhenTaskIsInvalid()
    {
        var controller = CreateController(new RecordingOpencodeClient());

        var result = await controller.HandleAIStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = new AITaskDto
            {
                Name = "Broken task",
                Cron = "invalid cron",
                Repository = "not-a-url",
                Prompt = string.Empty
            }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task HandleAIStructuredResponse_UpdateTask_ReturnsBadRequest_WhenTaskIsInvalid()
    {
        var repository = new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance);
        var task = await repository.CreateAsync(new Models.DTOs.CreateTaskDto
        {
            Name = "Existing task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/owner/repo",
            Prompt = "Do work"
        });
        var controller = CreateController(new RecordingOpencodeClient(), repository);

        var result = await controller.HandleAIStructuredResponse(new AIStructuredResponse
        {
            Action = AIActions.UpdateTask,
            TaskId = task.Id,
            Task = new AITaskDto
            {
                Name = "Broken update",
                Cron = "still invalid",
                Repository = "still-not-a-url",
                Prompt = string.Empty
            }
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static AIController CreateController(RecordingOpencodeClient opencodeClient, ITaskRepository? taskRepository = null)
    {
        return new AIController(
            taskRepository ?? new InMemoryTaskRepository(NullLogger<InMemoryTaskRepository>.Instance),
            new InMemorySchedulerService(),
            NullLogger<AIController>.Instance,
            opencodeClient,
            new ChatMessageRequestValidator(),
            new CreateTaskDtoValidator(),
            new UpdateTaskDtoValidator());
    }

    private sealed class RecordingOpencodeClient : IOpencodeClient
    {
        public bool Available { get; set; } = true;
        public Exception? CreateSessionException { get; set; }
        public string SendPromptResult { get; set; } = "Mock AI response";
        public string? WorkingDirectory { get; private set; }

        public bool IsServerAvailable() => Available;

        public Task<string> CreateSessionAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            WorkingDirectory = workingDirectory;
            if (CreateSessionException != null)
            {
                throw CreateSessionException;
            }

            return Task.FromResult("session-1");
        }

        public Task<string> SendPromptAsync(string sessionId, string prompt, string workingDirectory, CancellationToken cancellationToken = default)
        {
            WorkingDirectory = workingDirectory;
            return Task.FromResult(SendPromptResult);
        }

        public Task<string> SendPromptWithStreamingAsync(string sessionId, string prompt, string workingDirectory, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
            => Task.FromResult(SendPromptResult);

        public Task<List<FileDiff>> GetSessionDiffAsync(string sessionId, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<FileDiff>());

        public Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<List<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new List<SessionInfo>());

        public Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
            => Task.FromResult<SessionInfo?>(null);
    }
}
