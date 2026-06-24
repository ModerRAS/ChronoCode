using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Validators;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Direct unit tests for CreateTaskDtoValidator and UpdateTaskDtoValidator.
/// These are tested indirectly through controller tests, but direct tests
/// verify each validation rule independently.
/// </summary>
public class ValidatorTests
{
    private static CreateTaskDto ValidCreateDto() => new()
    {
        Name = "Test Task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        BranchStrategy = BranchStrategy.New,
        MaxRuntimeSeconds = 600,
        MaxFileChanges = 50,
        IsEnabled = true,
        WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, "do work"),
        MaxConcurrentRuns = 1,
        NodeFailurePolicyJson = "{}"
    };

    // ---- CreateTaskDtoValidator ----

    [Fact]
    public void Create_ValidDto_Passes()
    {
        var validator = new CreateTaskDtoValidator();
        var result = validator.Validate(ValidCreateDto());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Create_EmptyName_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.Name = "";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Create_NameTooLong_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.Name = new string('x', 101);
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Create_InvalidCron_TooFewParts_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.CronExpression = "0 0 *";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public void Create_EmptyCron_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.CronExpression = "";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public void Create_InvalidUrl_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.RepositoryUrl = "not-a-url";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RepositoryUrl");
    }

    [Fact]
    public void Create_EmptyWorkflow_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.WorkflowDefinitionJson = "";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WorkflowDefinitionJson");
    }

    [Fact]
    public void Create_InvalidWorkflow_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.WorkflowDefinitionJson = """{"version":1,"startNodeId":"nonexistent","nodes":[]}""";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WorkflowDefinitionJson");
    }

    [Fact]
    public void Create_OpencodeBackend_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.RuntimeBackend = "opencode";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RuntimeBackend");
    }

    [Fact]
    public void Create_PiBackend_Passes()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.RuntimeBackend = "pi";
        var result = validator.Validate(dto);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Create_NullBackend_Passes()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.RuntimeBackend = null;
        var result = validator.Validate(dto);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Create_MaxConcurrentRunsZero_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.MaxConcurrentRuns = 0;
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MaxConcurrentRuns");
    }

    [Fact]
    public void Create_MaxRuntimeZero_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.MaxRuntimeSeconds = 0;
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MaxRuntimeSeconds");
    }

    [Fact]
    public void Create_MaxFileChangesZero_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.MaxFileChanges = 0;
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MaxFileChanges");
    }

    [Fact]
    public void Create_InvalidFailurePolicy_Fails()
    {
        var validator = new CreateTaskDtoValidator();
        var dto = ValidCreateDto();
        dto.NodeFailurePolicyJson = "not json";
        var result = validator.Validate(dto);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "NodeFailurePolicyJson");
    }

    // ---- UpdateTaskDtoValidator ----

    [Fact]
    public void Update_EmptyDto_Passes()
    {
        // Update allows partial updates — empty dto should pass
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
    }

    [Fact]
    public void Update_ValidName_Passes()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { Name = "Updated" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_NameTooLong_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { Name = new string('x', 101) });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Name");
    }

    [Fact]
    public void Update_InvalidCron_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { CronExpression = "bad" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public void Update_InvalidUrl_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { RepositoryUrl = "not-a-url" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RepositoryUrl");
    }

    [Fact]
    public void Update_OpencodeBackend_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { RuntimeBackend = "opencode" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "RuntimeBackend");
    }

    [Fact]
    public void Update_PiBackend_Passes()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { RuntimeBackend = "pi" });
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Update_MaxConcurrentRunsZero_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto { MaxConcurrentRuns = 0 });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "MaxConcurrentRuns");
    }

    [Fact]
    public void Update_InvalidWorkflow_Fails()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto
        {
            WorkflowDefinitionJson = """{"version":1,"startNodeId":"nonexistent","nodes":[]}"""
        });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "WorkflowDefinitionJson");
    }

    [Fact]
    public void Update_ValidWorkflow_Passes()
    {
        var validator = new UpdateTaskDtoValidator();
        var result = validator.Validate(new UpdateTaskDto
        {
            WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, "do work")
        });
        Assert.True(result.IsValid);
    }
}
