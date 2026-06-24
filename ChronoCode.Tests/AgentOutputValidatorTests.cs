using System.Text.Json.Nodes;
using ChronoCode.Models.Workflow;
using ChronoCode.Services.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class AgentOutputValidatorTests
{
    private const string ValidEnvelope = """{"status":"completed","passed":true,"summary":"ok","artifacts":["file.ts"],"data":{"summary":"result"}}""";

    private static WorkflowDataContract Contract(params (string name, WorkflowDataType type, bool required)[] fields) =>
        new() { Fields = fields.Select(f => new WorkflowDataFieldContract { Name = f.name, Type = f.type, Required = f.required }).ToList() };

    [Fact]
    public void Validate_ValidEnvelope_ReturnsTrue()
    {
        var result = AgentOutputValidator.ValidateAgentOutput(ValidEnvelope, Contract(), out var envelope, out var error);
        Assert.True(result, error);
        Assert.Equal(string.Empty, error);
        Assert.NotNull(envelope);
    }

    [Fact]
    public void Validate_EmptyResponse_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_NullResponse_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput(null, Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_InvalidJson_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("not json", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("not valid JSON", error);
    }

    [Fact]
    public void Validate_MissingStatus_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"summary":"ok","artifacts":[],"data":{}}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("status", error);
    }

    [Fact]
    public void Validate_InvalidStatusValue_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"unknown","summary":"ok","artifacts":[],"data":{}}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("status", error);
    }

    [Fact]
    public void Validate_MissingSummary_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","artifacts":[],"data":{}}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("summary", error);
    }

    [Fact]
    public void Validate_MissingArtifacts_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","summary":"ok","data":{}}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("artifacts", error);
    }

    [Fact]
    public void Validate_ArtifactsNotArray_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","summary":"ok","artifacts":"notarray","data":{}}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("artifacts", error);
    }

    [Fact]
    public void Validate_MissingData_ReturnsFalse()
    {
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","summary":"ok","artifacts":[]}""", Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("data", error);
    }

    [Fact]
    public void Validate_MissingRequiredDataField_ReturnsFalse()
    {
        var contract = Contract(("summary", WorkflowDataType.String, true));
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","summary":"ok","artifacts":[],"data":{}}""", contract, out _, out var error);
        Assert.False(result);
        Assert.Contains("summary", error);
        Assert.Contains("required", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WrongTypeDataField_ReturnsFalse()
    {
        var contract = Contract(("count", WorkflowDataType.Number, true));
        var result = AgentOutputValidator.ValidateAgentOutput("""{"status":"completed","summary":"ok","artifacts":[],"data":{"count":"not-a-number"}}""", contract, out _, out var error);
        Assert.False(result);
        Assert.Contains("count", error);
        Assert.Contains("type", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_OptionalMissingDataField_ReturnsTrue()
    {
        var contract = Contract(("optional_field", WorkflowDataType.String, false));
        var result = AgentOutputValidator.ValidateAgentOutput(ValidEnvelope, contract, out _, out var error);
        Assert.True(result, error);
    }

    [Fact]
    public void Validate_PassedNullInEnvelope_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":null,"summary":"ok","artifacts":[],"data":{}}""";
        var result = AgentOutputValidator.ValidateAgentOutput(json, Contract(), out _, out var error);
        Assert.True(result, error);
    }

    [Fact]
    public void Validate_PassedNonBoolean_ReturnsFalse()
    {
        var json = """{"status":"completed","passed":"yes","summary":"ok","artifacts":[],"data":{}}""";
        var result = AgentOutputValidator.ValidateAgentOutput(json, Contract(), out _, out var error);
        Assert.False(result);
        Assert.Contains("passed", error);
    }

    [Fact]
    public void Validate_BooleanDataField_Matches()
    {
        var contract = Contract(("flag", WorkflowDataType.Boolean, true));
        var json = """{"status":"completed","summary":"ok","artifacts":[],"data":{"flag":true}}""";
        var result = AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out var error);
        Assert.True(result, error);
    }

    [Fact]
    public void Validate_ArrayDataField_Matches()
    {
        var contract = Contract(("items", WorkflowDataType.Array, true));
        var json = """{"status":"completed","summary":"ok","artifacts":[],"data":{"items":[1,2,3]}}""";
        var result = AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out var error);
        Assert.True(result, error);
    }

    [Fact]
    public void Validate_ObjectDataField_Matches()
    {
        var contract = Contract(("nested", WorkflowDataType.Object, true));
        var json = """{"status":"completed","summary":"ok","artifacts":[],"data":{"nested":{"key":"val"}}}""";
        var result = AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out var error);
        Assert.True(result, error);
    }
}
