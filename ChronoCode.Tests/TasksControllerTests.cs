using ChronoCode.Data;
using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using ChronoCode.Validators;
using FluentValidation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Net;
using System.Net.Http.Json;

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
            builder.ConfigureLogging(logging => logging.ClearProviders());
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

                services.RemoveAll<DatabaseRuntimeState>();
                services.AddSingleton(new DatabaseRuntimeState(DatabaseConfiguration.SqliteProvider, "Data Source=:memory:"));

                services.AddSingleton<ITaskRepository, InMemoryTaskRepository>();
                services.AddSingleton<IExecutionRepository, InMemoryExecutionRepository>();
                services.AddSingleton<ISchedulerService, InMemorySchedulerService>();
                services.AddSingleton<IOpencodeServerManager, InMemoryOpencodeServerManager>();
                services.AddSingleton<IOpencodeClient, InMemoryOpencodeClient>();
                services.AddSingleton<OpencodeRuntime>();
                services.AddSingleton<PiRuntime>();
                services.AddSingleton<InMemoryAgentRuntime>();
                services.AddSingleton<IAgentRuntime>(sp => sp.GetRequiredService<InMemoryAgentRuntime>());
                services.AddSingleton<IAgentRuntimeResolver>(sp => new InMemoryAgentRuntimeResolver(sp.GetRequiredService<InMemoryAgentRuntime>()));
                services.AddSingleton<IGitService, InMemoryGitService>();

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
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
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
            Name = "Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        await _client.PostAsJsonAsync("/api/tasks", dto);

        var response = await _client.GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<TaskDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(dto.Name, result[0].Name);
    }

    [Fact]
    public async Task Get_GetTaskById_ReturnsOkWhenTaskExists()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.GetAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(dto.Name, result.Name);
    }

    [Fact]
    public async Task Get_GetTaskById_ReturnsNotFoundWhenTaskDoesNotExist()
    {
        var response = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_UpdateTask_ReturnsOkWhenTaskExists()
    {
        var createDto = new CreateTaskDto
        {
            Name = "Original Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var updateDto = new UpdateTaskDto
        {
            Name = "Updated Task"
        };

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created!.Id}", updateDto);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<TaskDto>();
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Updated Task", result.Name);
    }

    [Fact]
    public async Task Put_UpdateTask_ReturnsNotFoundWhenTaskDoesNotExist()
    {
        var updateDto = new UpdateTaskDto
        {
            Name = "Updated Task"
        };

        var response = await _client.PutAsJsonAsync($"/api/tasks/{Guid.NewGuid()}", updateDto);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_DeleteTask_ReturnsNoContentWhenTaskExists()
    {
        var dto = new CreateTaskDto
        {
            Name = "Task To Delete",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.DeleteAsync($"/api/tasks/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await _client.GetAsync($"/api/tasks/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_DeleteTask_ReturnsNotFoundWhenTaskDoesNotExist()
    {
        var response = await _client.DeleteAsync($"/api/tasks/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_TriggerTask_ReturnsAcceptedWhenTaskExists()
    {
        var dto = new CreateTaskDto
        {
            Name = "Task To Trigger",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.PostAsync($"/api/tasks/{created!.Id}/run", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Post_TriggerTask_ReturnsNotFoundWhenTaskDoesNotExist()
    {
        var response = await _client.PostAsync($"/api/tasks/{Guid.NewGuid()}/run", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_Executions_ReturnsOkWithList()
    {
        var dto = new CreateTaskDto
        {
            Name = "Task With Executions",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var createResponse = await _client.PostAsJsonAsync("/api/tasks", dto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var response = await _client.GetAsync($"/api/tasks/{created!.Id}/executions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<List<ExecutionDto>>();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Post_CreateTask_ReturnsBadRequestWhenModelIsInvalid()
    {
        var dto = new CreateTaskDto
        {
            Name = string.Empty,
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_ReturnsBadRequestWhenRepositoryUrlIsInvalid()
    {
        var dto = new CreateTaskDto
        {
            Name = "Bad Repo Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "not-a-valid-url",
            WorkflowDefinitionJson = ChronoCode.Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(true, "Test prompt")
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CreateTask_ReturnsBadRequestWhenWorkflowDefinitionIsMissing()
    {
        var dto = new CreateTaskDto
        {
            Name = "Missing Workflow Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = ""
        };

        var response = await _client.PostAsJsonAsync("/api/tasks", dto);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_NodeSession_ReturnsPersistedMetadata()
    {
        var (execution, node) = await CreateNodeSessionAsync("mock-session", "mock-session-file", "/tmp/mock");

        var response = await _client.GetAsync($"/api/tasks/executions/{execution.Id}/nodes/{node.Id}/session");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<ExecutionSessionDto>();
        Assert.NotNull(session);
        Assert.True(session.SupportsSupplementalMessages);
        Assert.True(session.CanResume);
        Assert.Equal("mock-session", session.SessionId);
        Assert.Equal("mock-session-file", session.SessionFile);
    }

    [Fact]
    public async Task Post_NodeMessage_QueuesSupplementalMessage()
    {
        var (execution, node) = await CreateNodeSessionAsync("mock-session", "mock-session-file", "/tmp/mock");

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/nodes/{node.Id}/message",
            new ExecutionMessageDto { Message = "Keep going", Mode = "steer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ExecutionMessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("queued", payload.Result);
    }

    [Fact]
    public async Task Post_ResumeNodeSession_RestoresPersistedSession()
    {
        var (execution, node) = await CreateNodeSessionAsync("persisted-session", "persisted-session-file", "/tmp/persisted");

        InMemoryAgentRuntime.LiveSession = null;

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/nodes/{node.Id}/resume",
            new ResumeExecutionSessionDto());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<ExecutionSessionDto>();
        Assert.NotNull(session);
        Assert.True(session.IsLive);
        Assert.Equal("persisted-session", session.SessionId);
        Assert.Equal("persisted-session-file", session.SessionFile);
    }

    [Fact]
    public async Task Get_Nodes_ReturnsOkWithList()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        });
        var node = new ChronoCode.Models.Workflow.WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 0,
            Status = ChronoCode.Models.Workflow.WorkflowNodeStatus.Completed,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            AgentBackend = "pi"
        };
        await executionRepository.CreateNodeExecutionAsync(node);

        var response = await _client.GetAsync($"/api/tasks/executions/{execution.Id}/nodes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<List<NodeExecutionDto>>();
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("agent", result[0].NodeId);
        Assert.Equal("completed", result[0].Status);
    }

    [Fact]
    public async Task Post_ApproveNode_ReturnsOk()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        });
        var node = new ChronoCode.Models.Workflow.WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            NodeId = "gate",
            NodeType = "approval_gate",
            ScopeKey = "root",
            Attempt = 0,
            Status = ChronoCode.Models.Workflow.WorkflowNodeStatus.WaitingApproval,
            StartedAt = DateTime.UtcNow,
            AgentBackend = "pi"
        };
        await executionRepository.CreateNodeExecutionAsync(node);

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/approval/{node.Id}",
            new ApprovalRequestDto { Approved = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Post_RejectNode_ReturnsOk()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        });
        var node = new ChronoCode.Models.Workflow.WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            NodeId = "gate",
            NodeType = "approval_gate",
            ScopeKey = "root",
            Attempt = 0,
            Status = ChronoCode.Models.Workflow.WorkflowNodeStatus.WaitingApproval,
            StartedAt = DateTime.UtcNow,
            AgentBackend = "pi"
        };
        await executionRepository.CreateNodeExecutionAsync(node);

        var response = await _client.PostAsJsonAsync(
            $"/api/tasks/executions/{execution.Id}/approval/{node.Id}",
            new ApprovalRequestDto { Approved = false, Reason = "rejected by reviewer" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExecutionLogs_ReturnsOkWithList()
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        });

        var response = await _client.GetAsync($"/api/tasks/executions/{execution.Id}/logs");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task Get_ServerStatus_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tasks/server/status");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("backend", content.ToLower());
        Assert.Contains("running", content.ToLower());
    }

    [Fact]
    public async Task Post_StartServer_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/tasks/server/start", null);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("backend", content.ToLower());
    }

    [Fact]
    public async Task Post_StopServer_ReturnsOk()
    {
        var response = await _client.PostAsync("/api/tasks/server/stop", null);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Get_Nodes_EmptyExecution_ReturnsOkWithEmptyArray()
    {
        var response = await _client.GetAsync($"/api/tasks/executions/{Guid.NewGuid()}/nodes");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task Get_Executions_NonExistentTask_ReturnsOkWithEmptyArray()
    {
        var response = await _client.GetAsync($"/api/tasks/{Guid.NewGuid()}/executions");

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("[", content);
    }

    [Fact]
    public async Task Put_UpdateTask_WithInvalidCron_ReturnsBadRequest()
    {
        var createDto = new CreateTaskDto
        {
            Name = "Original Task",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null)
        };
        var createResponse = await _client.PostAsJsonAsync("/api/tasks", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<TaskDto>();

        var dto = new
        {
            name = "Updated",
            cronExpression = "bad-cron",
            repositoryUrl = "https://github.com/test/repo",
            baseBranch = "main",
            branchStrategy = 0,
            maxRuntimeSeconds = 600,
            maxFileChanges = 50,
            isEnabled = true,
            workflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            maxConcurrentRuns = 1,
            nodeFailurePolicyJson = "{}"
        };

        var response = await _client.PutAsJsonAsync($"/api/tasks/{created!.Id}", dto);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(TaskExecution execution, ChronoCode.Models.Workflow.WorkflowNodeExecution node)> CreateNodeSessionAsync(
        string sessionId, string sessionFile, string workingDirectory)
    {
        var executionRepository = _scope.ServiceProvider.GetRequiredService<IExecutionRepository>();
        var execution = await executionRepository.CreateAsync(new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = Models.TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        });
        var node = new ChronoCode.Models.Workflow.WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execution.Id,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 0,
            Status = ChronoCode.Models.Workflow.WorkflowNodeStatus.Running,
            StartedAt = DateTime.UtcNow,
            AgentBackend = "pi",
            AgentSessionId = sessionId,
            AgentSessionFile = sessionFile,
            AgentWorkingDirectory = workingDirectory
        };
        await executionRepository.CreateNodeExecutionAsync(node);
        return (execution, node);
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
    public Task SyncTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<ScheduledTask>());
    public Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken cancellationToken = default) => Task.FromResult(new List<DateTime>());
    public Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(new SchedulerQueueSnapshotDto());
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

public class InMemoryAgentRuntimeResolver : IAgentRuntimeResolver
{
    private readonly InMemoryAgentRuntime _runtime;
    public InMemoryAgentRuntimeResolver(InMemoryAgentRuntime runtime) => _runtime = runtime;
    public IAgentRuntime Get(string? backend) => _runtime;
    public AgentRuntimeStatus GetStatus(string? backend) => _runtime.GetStatus();
}
