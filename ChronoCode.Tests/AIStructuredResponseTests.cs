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
    public void AIStructuredResponse_CanSetProperties()
    {
        var taskDto = new CreateTaskDto
        {
            Name = "Test Task",
            CronExpression = "0 9 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            Prompt = "Test prompt"
        };

        var response = new AIStructuredResponse
        {
            Action = AIActions.CreateTask,
            Task = taskDto
        };

        Assert.Equal("create_task", response.Action);
        Assert.NotNull(response.Task);
        Assert.Equal("Test Task", response.Task.Name);
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
