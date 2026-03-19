using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ChronoDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ChronoDbContext>(options =>
                {
                    options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid().ToString());
                });

                services.AddScoped<ITaskRepository, InMemoryTaskRepository>();
                services.AddScoped<IExecutionRepository, InMemoryExecutionRepository>();
                services.AddSingleton<ISchedulerService, InMemorySchedulerService>();
                services.AddSingleton<IOpencodeServerManager, InMemoryOpencodeServerManager>();
                services.AddSingleton<IOpencodeClient, InMemoryOpencodeClient>();
                services.AddSingleton<IGitService, InMemoryGitService>();
                services.AddSingleton<ITaskRunner, InMemoryTaskRunner>();
                services.AddSingleton<ScheduledTaskJob>();

                services.AddValidatorsFromAssemblyContaining<CreateTaskDtoValidator>();
            });
        });

        _client = _factory.CreateClient();
        _scope = _factory.Services.CreateScope();
    }

    public void Dispose()
    {
        _scope?.Dispose();
        _client?.Dispose();
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
}

public class InMemorySchedulerService : ISchedulerService
{
    public void ScheduleTask(ScheduledTask task) { }
    public void UnscheduleTask(Guid taskId) { }
    public void TriggerTask(Guid taskId) { }
    public List<ScheduledTask> GetScheduledTasks() => new();
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
    public Task<string> SendTaskAsync(string prompt, CancellationToken cancellationToken = default)
        => Task.FromResult("Mock response");
}

public class InMemoryGitService : IGitService
{
    public Task<string> CloneRepositoryAsync(string repositoryUrl, string branchName)
        => Task.FromResult("/tmp/mock/path");
    public Task<string> CreateBranchAsync(string path, string branchName)
        => Task.FromResult(branchName);
    public Task CommitChangesAsync(string path, string message)
        => Task.CompletedTask;
    public Task<string> GetLatestCommitShaAsync(string path)
        => Task.FromResult("abc123");
    public Task<string> CreatePullRequestAsync(string path, string branchName, string title, string body)
        => Task.FromResult("https://github.com/mock/pr");
}

public class InMemoryTaskRunner : ITaskRunner
{
    public Task ExecuteTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}