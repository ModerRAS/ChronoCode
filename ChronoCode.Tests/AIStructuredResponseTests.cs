using System.Text.Json;
using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class AIStructuredResponseTests
{
    [Fact]
    public void AIActions_IsValid_ReturnsTrue_ForValidActions()
    {
        Assert.True(AIActions.IsValid("create_task"));
        Assert.True(AIActions.IsValid("update_task"));
        Assert.True(AIActions.IsValid("delete_task"));
        Assert.True(AIActions.IsValid("trigger_task"));
    }

    [Fact]
    public void AIActions_IsValid_ReturnsFalse_ForInvalidActions()
    {
        Assert.False(AIActions.IsValid("invalid_action"));
        Assert.False(AIActions.IsValid(""));
        Assert.False(AIActions.IsValid("CREATE_TASK"));
    }

    [Fact]
    public void AIStructuredResponse_DefaultValues_AreCorrect()
    {
        var response = new AIStructuredResponse();

        Assert.Equal(string.Empty, response.Action);
        Assert.Null(response.TaskId);
        Assert.Null(response.Task);
        Assert.Null(response.Error);
    }

    [Fact]
    public void AIStructuredResponse_SerializesAndDeserializes_SnakeCaseContract()
    {
        var original = new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            TaskId = null,
            Task = new AITaskDto
            {
                Name = "AI Task",
                Cron = "0 2 * * *",
                Repository = "https://github.com/owner/repo",
                BaseBranch = "main",
                BranchStrategy = "reuse",
                MaxRuntimeSeconds = 120,
                MaxFileChanges = 7,
                IsEnabled = false,
                WorkflowDefinitionJson = "{}",
                DefaultInputsJson = null,
                RuntimeBackend = "pi",
                MaxConcurrentRuns = 2,
                NodeFailurePolicyJson = "{}"
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AIStructuredResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(AIActions.CreateTask, deserialized!.Action);
        Assert.NotNull(deserialized.Task);
        Assert.Equal("AI Task", deserialized.Task!.Name);
        Assert.Equal("0 2 * * *", deserialized.Task.Cron);
        Assert.Equal("https://github.com/owner/repo", deserialized.Task.Repository);
        Assert.Equal("reuse", deserialized.Task.BranchStrategy);
        Assert.Equal("pi", deserialized.Task.RuntimeBackend);
        Assert.Equal(2, deserialized.Task.MaxConcurrentRuns);

        var taskJson = JsonDocument.Parse(json).RootElement.GetProperty("task").GetRawText();
        Assert.Contains("workflow_definition_json", taskJson);
        Assert.Contains("runtime_backend", taskJson);
        Assert.Contains("max_concurrent_runs", taskJson);
    }

    [Fact]
    public void AIError_CanSetProperties()
    {
        var error = new AIError { Code = "INFO", Message = "helpful response" };
        Assert.Equal("INFO", error.Code);
        Assert.Equal("helpful response", error.Message);
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_MapsWorkflowFields()
    {
        var dto = new AITaskDto
        {
            Name = "AI Task",
            Cron = "0 2 * * *",
            Repository = "https://github.com/owner/repo",
            BaseBranch = "develop",
            BranchStrategy = "reuse",
            MaxRuntimeSeconds = 300,
            MaxFileChanges = 10,
            IsEnabled = true,
            WorkflowDefinitionJson = WorkflowDefinitionSerializer.Serialize(WorkflowDefinitionFactory.CreateDefault(false, null)),
            DefaultInputsJson = "{\"key\":\"value\"}",
            RuntimeBackend = "pi",
            MaxConcurrentRuns = 3,
            NodeFailurePolicyJson = "{}"
        };

        var createDto = dto.ToCreateTaskDto();

        Assert.Equal("AI Task", createDto.Name);
        Assert.Equal("0 2 * * *", createDto.CronExpression);
        Assert.Equal("https://github.com/owner/repo", createDto.RepositoryUrl);
        Assert.Equal("develop", createDto.BaseBranch);
        Assert.Equal(Models.BranchStrategy.Reuse, createDto.BranchStrategy);
        Assert.Equal(300, createDto.MaxRuntimeSeconds);
        Assert.Equal("pi", createDto.RuntimeBackend);
        Assert.Equal(3, createDto.MaxConcurrentRuns);
        Assert.False(string.IsNullOrWhiteSpace(createDto.WorkflowDefinitionJson));
        Assert.True(WorkflowDefinitionValidator.IsValid(createDto.WorkflowDefinitionJson, out _));
    }

    [Fact]
    public void AITaskDto_ToCreateTaskDto_DefaultsWorkflowWhenMissing()
    {
        var dto = new AITaskDto
        {
            Name = "Task",
            Cron = "0 2 * * *",
            Repository = "https://github.com/owner/repo"
        };

        var createDto = dto.ToCreateTaskDto();

        Assert.False(string.IsNullOrWhiteSpace(createDto.WorkflowDefinitionJson));
        Assert.True(WorkflowDefinitionValidator.IsValid(createDto.WorkflowDefinitionJson, out var error), error);
    }
}
