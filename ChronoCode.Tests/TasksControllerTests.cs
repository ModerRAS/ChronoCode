using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace ChronoCode.Tests;

public class TasksControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly IServiceScope _scope;

    public TasksControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = DatabaseConfiguration.SqliteProvider,
                    ["ConnectionStrings:SqliteConnection"] = "Data Source=:memory:",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ChronoDbContext>();
                services.RemoveAll<DbContextOptions<ChronoDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ChronoDbContext>>();

                var providerDescriptors = services
                    .Where(d =>
                        (d.ServiceType.Assembly.GetName().Name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (d.ImplementationType?.Assembly.GetName().Name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false)
                        || (d.ImplementationInstance?.GetType().Assembly.GetName().Name?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();

                foreach (var providerDescriptor in providerDescriptors)
                {
                    services.Remove(providerDescriptor);
                }

                services.AddDbContext<ChronoDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString());
                });

                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
                services.AddSingleton<IExecutionRepository, InMemoryExecutionRepository>();
                services.AddSingleton<ISchedulerService, InMemorySchedulerService>();
                services.AddSingleton<IOpencodeServerManager, InMemoryOpencodeServerManager>();
                services.AddSingleton<IOpencodeClient, InMemoryOpencodeClient>();
                services.AddSingleton<OpencodeRuntime>();
                services.AddSingleton<PiRuntime>();
                services.AddSingleton<IAgentRuntime, InMemoryAgentRuntime>();
                services.AddSingleton<IGitService, InMemoryGitService>();
                services.AddSingleton<ITaskRunner, InMemoryTaskRunner>();
                services.AddScoped<ScheduledTaskJob>();

                services.AddValidatorsFromAssemblyContaining<CreateTaskDtoValidator>();
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
        InMemoryAgentRuntime.LiveSession = new AgentExecutionSession("pi", "mock-session", "mock-session-file", "/tmp/mock", true);
    }

    public void Dispose()
    {
        _scope.Dispose();
        _client.Dispose();
    }

    [Fact]
    public async Task Post_CreateTask_Returns201Created()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(result);
        Assert.Equal(dto.Name, result.Name);
        Assert.Equal(dto.CronExpression, result.CronExpression);
    }

    [Fact]
    public async Task Get_GetTasks_ReturnsOkWithList()
    {
        var dto = new CreateTaskDto
        {
            Name = "Get Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        await _client.PostAsJsonAsync("/api/tasks", dto);

        var response = await _client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.NotNull(tasks);
        Assert.NotEmpty(tasks);
    }

    [Fact]
    public async Task Get_GetTask_ReturnsTask_WhenExists()
    {
        var dto = new CreateTaskDto
        {
            Name = "Get By Id Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.GetAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var task = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(task);
        Assert.Equal(created.Id, task.Id);
    }

    [Fact]
    public async Task Get_GetTask_Returns404_WhenNotExists()
    {
        var response = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdateTask_ReturnsUpdatedTask()
    {
        var createDto = new CreateTaskDto
        {
            Name = "Update Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var updateDto = new UpdateTaskDto
        {
            Name = "Updated Task Name",
            CronExpression = "0 12 * * *"
        };

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created!.Id}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Task Name", updated.Name);
        Assert.Equal("0 12 * * *", updated.CronExpression);
    }

    [Fact]
    public async Task Put_UpdateTask_Returns404_WhenNotExists()
    {
        var updateDto = new UpdateTaskDto
        {
            Name = "Updated Task Name"
        };

        var response = await _client.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}", updateDto);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_DeleteTask_Returns204_WhenExists()
    {
        var dto = new CreateTaskDto
        {
            Name = "Delete Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.DeleteAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_DeleteTask_Returns404_WhenNotExists()
    {
        var response = await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownApiRoute_Returns404_WhenPathStartsWithApi()
    {
        var response = await _client.GetAsync("/api/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_UnknownStaticAsset_Returns404_WhenFileIsMissing()
    {
        var response = await _client.GetAsync("/assets/does-not-exist.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenNameIsEmpty()
    {
        var dto = new CreateTaskDto
        {
            Name = "",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenCronIsInvalid()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "invalid-cron",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenCronHasTooFewParts()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 0 * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenUrlIsInvalid()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "not-a-valid-url",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenPromptIsEmpty()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = ""
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_Returns400_WhenNameIsTooLong()
    {
        var dto = new CreateTaskDto
        {
            Name = new string('a', 101),
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExecutionSession_ReturnsLiveSession()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(Guid.NewGuid());
        await executionRepository.UpdateSessionAsync(execution.Id, new AgentExecutionSession("pi", "mock-session", "mock-session-file", "/tmp/mock", true));

        var response = await _client.GetAsync($"/api/tasks/executions/{execution.Id}/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<ExecutionSessionDto>();
        Assert.NotNull(session);
        Assert.True(session.IsLive);
        Assert.True(session.SupportsSupplementalMessages);
        Assert.Equal("mock-session", session.SessionId);
    }

    [Fact]
    public async Task Post_ExecutionMessage_QueuesSupplementalMessage()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(Guid.NewGuid());
        await executionRepository.UpdateSessionAsync(execution.Id, new AgentExecutionSession("pi", "mock-session", "mock-session-file", "/tmp/mock", true));

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/message",
            new ExecutionMessageDto { Message = "Keep going", Mode = "steer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ExecutionMessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("queued", payload.Result);
    }

    [Fact]
    public async Task Post_ResumeExecutionSession_RestoresPersistedSession()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(Guid.NewGuid());
        await executionRepository.UpdateSessionAsync(execution.Id, new AgentExecutionSession("pi", "persisted-session", "persisted-session-file", "/tmp/persisted", true));

        InMemoryAgentRuntime.LiveSession = null;

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/resume",
            new ResumeExecutionSessionDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<ExecutionSessionDto>();
        Assert.NotNull(session);
        Assert.True(session.IsLive);
        Assert.Equal("persisted-session", session.SessionId);
        Assert.Equal("persisted-session-file", session.SessionFile);
    }
}

public class ExecutionMessageResponse
{
    public Guid ExecutionId { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? SessionFile { get; set; }
}

public class InMemorySchedulerService : ISchedulerService
{
    public void ScheduleTask(ScheduledTask task) { }
    public void UnscheduleTask(Guid taskId) { }
    public void TriggerTask(Guid taskId) { }
    public Task<List<ScheduledTask>> GetScheduledTasksAsync() => Task.FromResult(new List<ScheduledTask>());
    public List<DateTime> GetNextRunTimes(Guid taskId, int count = 5) => new();
}

public class InMemoryOpencodeServerManager : IOpencodeServerManager
{
    public Task StartServerAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopServerAsync() => Task.CompletedTask;
    public bool IsServerRunning => false;
    public string ServerUrl => "http://localhost:4096";
    public Task<bool> WaitForServerReadyAsync(TimeSpan timeout) => Task.FromResult(true);
}

public class InMemoryOpencodeClient : IOpencodeClient
{
    public bool IsServerAvailable() => true;

    public Task<string> CreateSessionAsync(string workingDirectory, CancellationToken cancellationToken = default)
        => Task.FromResult("mock-session-id");
    
    public Task<string> SendPromptAsync(string sessionId, string prompt, string workingDirectory, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock AI response");
    
    public Task<string> SendPromptWithStreamingAsync(string sessionId, string prompt, string workingDirectory, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock streaming response");
    
    public Task<List<FileDiff>> GetSessionDiffAsync(string sessionId, string? messageId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new List<FileDiff>());
    
    public Task AbortSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    
    public Task<List<SessionInfo>> ListSessionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new List<SessionInfo>());
    
    public Task<SessionInfo?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<SessionInfo?>(null);
}

public class InMemoryGitService : IGitService
{
    public Task<string> CloneRepositoryAsync(string repoUrl, string workspacePath)
        => Task.FromResult("/tmp/mock/repo");
    
    public Task<string> CreateBranchAsync(string repoPath, string branchName, string baseBranch)
        => Task.FromResult(branchName);
    
    public Task CheckoutBranchAsync(string repoPath, string branchName)
        => Task.CompletedTask;
    
    public Task<string> CommitChangesAsync(string repoPath, string message)
        => Task.FromResult("mock-commit-sha");
    
    public Task PushChangesAsync(string repoPath, string remoteName = "origin")
        => Task.CompletedTask;
    
    public Task<string> CreatePullRequestAsync(string repoPath, string branchName, string baseBranch, string title, string body)
        => Task.FromResult("https://github.com/mock/pr/1");
    
    public Task<List<GitFileStatus>> GetChangedFilesAsync(string repoPath)
        => Task.FromResult(new List<GitFileStatus>());
}

public class InMemoryTaskRunner : ITaskRunner
{
    public Task<TaskExecution> ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        var execution = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = task.Id,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = Models.TaskStatus.Completed,
            BranchName = "mock-branch",
            CommitSha = "mock-sha",
            FilesChanged = 0
        };
        return Task.FromResult(execution);
    }
}

public class InMemoryAgentRuntime : IAgentRuntime
{
    public static AgentExecutionSession? LiveSession { get; set; } = new("pi", "mock-session", "mock-session-file", "/tmp/mock", true);

    public AgentRuntimeStatus GetStatus() => new("pi", true, null, true, true, true);

    public Task EnsureReadyAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<AgentExecutionSession> EnsureExecutionSessionAsync(Guid executionId, string workingDirectory, Func<string, Task> onChunk, string? sessionRef = null, CancellationToken cancellationToken = default)
        => Task.FromResult(LiveSession ?? new AgentExecutionSession("pi", "mock-session", "mock-session-file", workingDirectory, true));

    public Task<string> SendMessageAsync(Guid executionId, string workingDirectory, string prompt, AgentMessageMode mode, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
        => Task.FromResult(mode == AgentMessageMode.Prompt ? "mock prompt result" : "queued");

    public Task<AgentExecutionSession?> GetExecutionSessionAsync(Guid executionId, CancellationToken cancellationToken = default)
        => Task.FromResult(LiveSession);

    public Task<AgentExecutionSession> ResumeExecutionSessionAsync(Guid executionId, string workingDirectory, string sessionRef, Func<string, Task> onChunk, CancellationToken cancellationToken = default)
    {
        var resumed = sessionRef.Contains("file", StringComparison.OrdinalIgnoreCase)
            ? new AgentExecutionSession("pi", "persisted-session", sessionRef, workingDirectory, true)
            : new AgentExecutionSession("pi", sessionRef, null, workingDirectory, true);

        LiveSession = resumed;
        return Task.FromResult(resumed);
    }

    public Task StopExecutionAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        LiveSession = null;
        return Task.CompletedTask;
    }
}
