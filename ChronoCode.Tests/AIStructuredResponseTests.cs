using System.Text.Json;
using ChronoCode.Models.AI;
using ChronoCode.Models.DTOs;
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
        const string json = """
            {
              "action": "create_task",
              "task_id": null,
              "task": {
                "name": "Test Task",
                "cron": "0 9 * * *",
                "repository": "https://github.com/test/repo",
                "base_branch": "main",
                "branch_strategy": "reuse",
                "prompt": "Test prompt",
                "max_runtime_seconds": 120,
                "max_file_changes": 7,
                "require_plan_review": false,
                "is_enabled": true
              },
              "error": null
            }
            """;

        var response = JsonSerializer.Deserialize<AIStructuredResponse>(json);

        Assert.NotNull(response);
        Assert.Equal("create_task", response.Action);
        Assert.Null(response.TaskId);
        Assert.NotNull(response.Task);
        Assert.Equal("reuse", response.Task.BranchStrategy);
        Assert.Equal(120, response.Task.MaxRuntimeSeconds);

        var serialized = JsonSerializer.Serialize(response);

        Assert.Contains("\"task_id\":null", serialized);
        Assert.Contains("\"base_branch\":\"main\"", serialized);
        Assert.Contains("\"branch_strategy\":\"reuse\"", serialized);
        Assert.DoesNotContain("\"TaskId\"", serialized);
        Assert.DoesNotContain("\"BaseBranch\"", serialized);
    }

    [Fact]
    public void AIError_CanSetProperties()
    {
        var error = new AIError
        {
            Code = "VALIDATION_ERROR",
            Message = "Invalid input"
        };

        Assert.Equal("VALIDATION_ERROR", error.Code);
        Assert.Equal("Invalid input", error.Message);
    }

    [Fact]
    public void CreateTaskDto_RequiredFields_CanBeSet()
    {
        var dto = new CreateTaskDto
        {
            Name = "AI Task",
            CronExpression = "0 2 * * 1",
            RepositoryUrl = "https://github.com/owner/repo",
            Prompt = "Check TODO comments"
        };

        Assert.Equal("AI Task", dto.Name);
        Assert.Equal("0 2 * * 1", dto.CronExpression);
        Assert.Equal("https://github.com/owner/repo", dto.RepositoryUrl);
        Assert.Equal("Check TODO comments", dto.Prompt);
    }
}
