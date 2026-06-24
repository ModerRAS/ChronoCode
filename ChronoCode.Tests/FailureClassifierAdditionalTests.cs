using ChronoCode.Models.Workflow;
using ChronoCode.Services.Workflow;
using System.Text.Json.Nodes;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Additional FailureClassifier tests for uncovered exception types,
/// and AgentOutputValidator tests for extra edge cases.
/// </summary>
public class FailureClassifierAdditionalTests
{
    // ---- FailureClassifier: more exception types ----

    [Fact]
    public void Classify_TaskCanceledException_ReturnsTimeout()
    {
        // TaskCanceledException inherits from OperationCanceledException
        Assert.Equal(WorkflowRetryReason.Timeout, FailureClassifier.Classify(new TaskCanceledException()));
    }

    [Fact]
    public void Classify_FormatException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new FormatException()));
    }

    [Fact]
    public void Classify_JsonException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new System.Text.Json.JsonException()));
    }

    [Fact]
    public void Classify_NotSupportedException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new NotSupportedException()));
    }

    [Fact]
    public void Classify_KeyNotFoundException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new System.Collections.Generic.KeyNotFoundException()));
    }

    [Fact]
    public void Classify_EndOfStreamException_ReturnsTransportError()
    {
        // EndOfStreamException inherits from IOException
        Assert.Equal(WorkflowRetryReason.TransportError, FailureClassifier.Classify(new System.IO.EndOfStreamException()));
    }

    [Fact]
    public void Classify_AggregateException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new AggregateException("multi")));
    }

    [Fact]
    public void Classify_HttpRequestException_WithMessage_ReturnsTransportError()
    {
        var ex = new HttpRequestException("connection refused");
        Assert.Equal(WorkflowRetryReason.TransportError, FailureClassifier.Classify(ex));
    }

    // ---- AgentOutputValidator: extra edge cases ----

    [Fact]
    public void Validate_StatusBlocked_ReturnsTrue()
    {
        var json = """{"status":"blocked","passed":false,"summary":"blocked by user","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_StatusFailed_ReturnsTrue()
    {
        var json = """{"status":"failed","passed":false,"summary":"task failed","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_PassedTrue_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_PassedFalse_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":false,"summary":"not passed","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_PassedNull_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":null,"summary":"done","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_EmptyArtifacts_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_WithArtifacts_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":["file1.ts","file2.ts"],"data":{}}""";
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, null, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_MissingAllFields_ReturnsFalse()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [new() { Name = "plan", Type = WorkflowDataType.String, Required = true }]
        };
        Assert.False(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_AllFieldsPresent_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{"plan":"do stuff"}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [new() { Name = "plan", Type = WorkflowDataType.String, Required = true }]
        };
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_OptionalFieldMissing_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{"plan":"do stuff"}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [
                new() { Name = "plan", Type = WorkflowDataType.String, Required = true },
                new() { Name = "notes", Type = WorkflowDataType.String, Required = false }
            ]
        };
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_NumberTypeMismatch_ReturnsFalse()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{"count":"not-a-number"}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [new() { Name = "count", Type = WorkflowDataType.Number, Required = true }]
        };
        Assert.False(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_NumberTypeValid_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{"count":42}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [new() { Name = "count", Type = WorkflowDataType.Number, Required = true }]
        };
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }

    [Fact]
    public void Validate_WithDataContract_BooleanTypeValid_ReturnsTrue()
    {
        var json = """{"status":"completed","passed":true,"summary":"done","artifacts":[],"data":{"flag":true}}""";
        var contract = new WorkflowDataContract
        {
            Fields = [new() { Name = "flag", Type = WorkflowDataType.Boolean, Required = true }]
        };
        Assert.True(AgentOutputValidator.ValidateAgentOutput(json, contract, out _, out _));
    }
}
