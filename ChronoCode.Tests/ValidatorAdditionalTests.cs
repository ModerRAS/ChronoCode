using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Validators;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Tests for ChatMessageRequestValidator and UpdateRuntimeSettingsDtoValidator.
/// Also: CreateTaskDtoValidator edge cases and UpdateTaskDtoValidator edge cases.
/// </summary>
public class ValidatorAdditionalTests
{
    // ---- ChatMessageRequestValidator ----

    [Fact]
    public void ChatMessage_ValidMessage_Passes()
    {
        var validator = new ChatMessageRequestValidator();
        var result = validator.Validate(new ChatMessageRequest { Message = "hello" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ChatMessage_EmptyMessage_Fails()
    {
        var validator = new ChatMessageRequestValidator();
        var result = validator.Validate(new ChatMessageRequest { Message = "" });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ChatMessage_WhitespaceMessage_Fails()
    {
        var validator = new ChatMessageRequestValidator();
        var result = validator.Validate(new ChatMessageRequest { Message = "   " });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ChatMessage_NullMessage_Fails()
    {
        var validator = new ChatMessageRequestValidator();
        var result = validator.Validate(new ChatMessageRequest { Message = null! });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ChatMessage_LongMessage_Passes()
    {
        var validator = new ChatMessageRequestValidator();
        var result = validator.Validate(new ChatMessageRequest { Message = new string('a', 10000) });
        Assert.True(result.IsValid);
    }

    // ---- UpdateRuntimeSettingsDtoValidator ----

    [Fact]
    public void RuntimeSettings_PiBackend_Passes()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 3000 }
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_OpencodeBackend_Passes()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "opencode" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 3000 }
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_InvalidBackend_Fails()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "invalid" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 3000 }
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_PortZero_Fails()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 0 }
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_PortNegative_Fails()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = -1 }
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_PortTooHigh_Fails()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 70000 }
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_PortMax_Valid()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 65535 }
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RuntimeSettings_PortMin_Valid()
    {
        var validator = new UpdateRuntimeSettingsDtoValidator();
        var result = validator.Validate(new UpdateRuntimeSettingsDto
        {
            AgentRuntime = new UpdateAgentRuntimeSettingsDto { Backend = "pi" },
            Opencode = new UpdateOpencodeSettingsDto { Port = 1 }
        });
        Assert.True(result.IsValid);
    }

    // ---- CreateTaskDtoValidator edge cases ----

    [Fact]
    public void Create_MaxConcurrentRunsNegative_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = -1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_MaxRuntimeNegative_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = -1,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_MaxFileChangesNegative_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = -1,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_ValidFivePartCron_Passes()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "*/5 * * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Create_SixPartCron_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Create_FourPartCron_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.False(result.IsValid);
    }

    // ---- UpdateTaskDtoValidator edge cases ----

    [Fact]
    public void Update_DisabledTask_Passes()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto
        {
            Name = "Disabled Task",
            IsEnabled = false,
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_EmptyRepository_Passes_PartialUpdate()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto
        {
            Name = "Test",
            RepositoryUrl = "",
            CronExpression = "0 0 * * *",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, null),
            NodeFailurePolicyJson = "{}"
        });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_NullWorkflow_Passes_PartialUpdate()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/test/repo",
            MaxConcurrentRuns = 1,
            MaxRuntimeSeconds = 600,
            MaxFileChanges = 50,
            WorkflowDefinitionJson = null!,
            NodeFailurePolicyJson = "{}"
        });
        Assert.True(result.IsValid);
    }
}
