using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services.Workflow;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Direct unit tests for WorkflowContext: path resolution, predicate evaluation,
/// node output storage, variable management, scope key computation, and
/// serialize/deserialize roundtrip.
/// </summary>
public class WorkflowContextTests
{
    private static WorkflowContext CreateContext()
    {
        var ctx = new WorkflowContext();
        ctx.Root["task"] = new JsonObject { ["name"] = "my-task" };
        ctx.Root["run"] = new JsonObject { ["workspacePath"] = "/tmp/ws" };
        ctx.Root["inputs"] = new JsonObject { ["proceed"] = true, ["count"] = 5, ["name"] = "hello", ["items"] = new JsonArray("a", "b", "c") };
        ctx.Root["nodes"] = new JsonObject();
        return ctx;
    }

    // ---- ResolvePath ----

    [Fact]
    public void ResolvePath_NullOrEmpty_ReturnsNull()
    {
        var ctx = CreateContext();
        Assert.Null(ctx.ResolvePath(null));
        Assert.Null(ctx.ResolvePath(""));
        Assert.Null(ctx.ResolvePath("   "));
    }

    [Fact]
    public void ResolvePath_RootDollar_ReturnsRoot()
    {
        var ctx = CreateContext();
        Assert.NotNull(ctx.ResolvePath("$"));
    }

    [Fact]
    public void ResolvePath_SimpleProperty_ReturnsValue()
    {
        var ctx = CreateContext();
        var result = ctx.ResolvePath("$.task.name");
        Assert.NotNull(result);
        Assert.Equal("my-task", result!.GetValue<string>());
    }

    [Fact]
    public void ResolvePath_NestedObject_ReturnsValue()
    {
        var ctx = CreateContext();
        var result = ctx.ResolvePath("$.run.workspacePath");
        Assert.Equal("/tmp/ws", result!.GetValue<string>());
    }

    [Fact]
    public void ResolvePath_ArrayIndex_ReturnsElement()
    {
        var ctx = CreateContext();
        var result = ctx.ResolvePath("$.inputs.items[1]");
        Assert.Equal("b", result!.GetValue<string>());
    }

    [Fact]
    public void ResolvePath_MissingSegment_ReturnsNull()
    {
        var ctx = CreateContext();
        Assert.Null(ctx.ResolvePath("$.task.nonexistent"));
    }

    [Fact]
    public void ResolvePath_MissingIntermediateSegment_ReturnsNull()
    {
        var ctx = CreateContext();
        Assert.Null(ctx.ResolvePath("$.nonexistent.deep.path"));
    }

    [Fact]
    public void ResolvePath_NodeOutput_ReturnsValue()
    {
        var ctx = CreateContext();
        ctx.SetNodeOutput("agent1", JsonNode.Parse("""{"passed":true,"summary":"ok"}"""));
        var result = ctx.ResolvePath("$.nodes.agent1.output.passed");
        Assert.NotNull(result);
        Assert.True(result!.GetValue<bool>());
    }

    [Fact]
    public void ResolvePath_NodeOutputSummary_ReturnsString()
    {
        var ctx = CreateContext();
        ctx.SetNodeOutput("agent1", JsonNode.Parse("""{"passed":true,"summary":"did work"}"""));
        var result = ctx.ResolvePath("$.nodes.agent1.output.summary");
        Assert.Equal("did work", result!.GetValue<string>());
    }

    // ---- EvaluatePredicate ----

    [Fact]
    public void EvaluatePredicate_ConstantTrue_ReturnsTrue()
    {
        var ctx = CreateContext();
        Assert.True(ctx.EvaluatePredicate(new ConstantWorkflowPredicate { Value = true }));
    }

    [Fact]
    public void EvaluatePredicate_ConstantFalse_ReturnsFalse()
    {
        var ctx = CreateContext();
        Assert.False(ctx.EvaluatePredicate(new ConstantWorkflowPredicate { Value = false }));
    }

    [Fact]
    public void EvaluatePredicate_NullPredicate_ReturnsFalse()
    {
        var ctx = CreateContext();
        Assert.False(ctx.EvaluatePredicate(null));
    }

    [Fact]
    public void EvaluatePredicate_Truthy_OnBooleanTrue_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.proceed", Operator = WorkflowComparisonOperator.Truthy };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Truthy_OnNumberNonZero_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.count", Operator = WorkflowComparisonOperator.Truthy };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Truthy_OnNonEmptyString_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.name", Operator = WorkflowComparisonOperator.Truthy };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Falsy_OnBooleanTrue_ReturnsFalse()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.proceed", Operator = WorkflowComparisonOperator.Falsy };
        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Exists_OnPresentPath_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.proceed", Operator = WorkflowComparisonOperator.Exists };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Exists_OnMissingPath_ReturnsFalse()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.nonexistent", Operator = WorkflowComparisonOperator.Exists };
        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_NotExists_OnMissingPath_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.nonexistent", Operator = WorkflowComparisonOperator.NotExists };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Equals_OnMatchingValue_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.name", Operator = WorkflowComparisonOperator.Equals, Value = JsonNode.Parse("\"hello\"") };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Equals_OnNonMatchingValue_ReturnsFalse()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.name", Operator = WorkflowComparisonOperator.Equals, Value = JsonNode.Parse("\"world\"") };
        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_NotEquals_OnNonMatchingValue_ReturnsTrue()
    {
        var ctx = CreateContext();
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.name", Operator = WorkflowComparisonOperator.NotEquals, Value = JsonNode.Parse("\"world\"") };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Equals_CompareToPath_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("expected", "hello");
        var pred = new ComparisonWorkflowPredicate { Path = "$.inputs.name", Operator = WorkflowComparisonOperator.Equals, CompareToPath = "$.expected" };
        Assert.True(ctx.EvaluatePredicate(pred));
    }

    // ---- SetNodeOutput / GetNodeOutput ----

    [Fact]
    public void SetNodeOutput_StoresUnderNodesNodeIdOutput()
    {
        var ctx = CreateContext();
        ctx.SetNodeOutput("agent1", JsonNode.Parse("""{"passed":true}"""));

        var output = ctx.GetNodeOutput("agent1");
        Assert.NotNull(output);
        Assert.True(output!["passed"]!.GetValue<bool>());
    }

    [Fact]
    public void SetNodeOutput_Null_RemovesOutput()
    {
        var ctx = CreateContext();
        ctx.SetNodeOutput("agent1", JsonNode.Parse("""{"passed":true}"""));
        ctx.SetNodeOutput("agent1", null);

        Assert.Null(ctx.GetNodeOutput("agent1"));
    }

    [Fact]
    public void GetNodeOutput_MissingNode_ReturnsNull()
    {
        var ctx = CreateContext();
        Assert.Null(ctx.GetNodeOutput("nonexistent"));
    }

    // ---- SetVariable / GetVariable ----

    [Fact]
    public void SetVariable_StoresAtRootLevel()
    {
        var ctx = CreateContext();
        ctx.SetVariable("myvar", JsonNode.Parse("\"value\""));

        var result = ctx.ResolvePath("$.myvar");
        Assert.Equal("value", result!.GetValue<string>());
    }

    [Fact]
    public void SetVariable_Null_RemovesVariable()
    {
        var ctx = CreateContext();
        ctx.SetVariable("myvar", JsonNode.Parse("\"value\""));
        ctx.SetVariable("myvar", null);

        Assert.Null(ctx.ResolvePath("$.myvar"));
    }

    // ---- ComputeScopeKey ----

    [Fact]
    public void ComputeScopeKey_NoFrames_ReturnsRoot()
    {
        var ctx = CreateContext();
        Assert.Equal("root", ctx.ComputeScopeKey());
    }

    [Fact]
    public void ComputeScopeKey_ForEachFrame_IncludesIndex()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "for_each", NodeId = "fe", Index = 2 });
        Assert.Equal("fe#2", ctx.ComputeScopeKey());
    }

    [Fact]
    public void ComputeScopeKey_ParallelFrame_IncludesBranchIndex()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "parallel", NodeId = "par", BranchIndex = 1 });
        Assert.Equal("par#1", ctx.ComputeScopeKey());
    }

    [Fact]
    public void ComputeScopeKey_WhileFrame_IncludesCount()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "while", NodeId = "wh", Count = 3 });
        Assert.Equal("wh#3", ctx.ComputeScopeKey());
    }

    [Fact]
    public void ComputeScopeKey_MultipleFrames_JoinWithPipe()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "for_each", NodeId = "fe", Index = 1 });
        ctx.Frames.Add(new WorkflowFrame { Type = "parallel", NodeId = "par", BranchIndex = 0 });
        Assert.Equal("fe#1|par#0", ctx.ComputeScopeKey());
    }

    // ---- Serialize / Deserialize ----

    [Fact]
    public void SerializeDeserialize_RoundtripsContext()
    {
        var ctx = CreateContext();
        ctx.SetNodeOutput("agent1", JsonNode.Parse("""{"passed":true}"""));
        ctx.SetVariable("item", JsonNode.Parse("\"hello\""));
        ctx.Frames.Add(new WorkflowFrame { Type = "for_each", NodeId = "fe", Index = 1, ItemVariable = "item", BodyStart = "body", Next = "end", MaxIter = 5 });

        var json = ctx.Serialize();
        var round = WorkflowContext.Deserialize(json);

        Assert.NotNull(round);
        Assert.Equal("my-task", round!.ResolvePath("$.task.name")!.GetValue<string>());
        Assert.True(round.GetNodeOutput("agent1")!["passed"]!.GetValue<bool>());
        Assert.Equal("hello", round.ResolvePath("$.item")!.GetValue<string>());
        Assert.Single(round.Frames);
        Assert.Equal("fe", round.Frames[0].NodeId);
        Assert.Equal(1, round.Frames[0].Index);
    }

    [Fact]
    public void Deserialize_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(WorkflowContext.Deserialize(null));
        Assert.Null(WorkflowContext.Deserialize(""));
        Assert.Null(WorkflowContext.Deserialize("   "));
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsNull()
    {
        Assert.Null(WorkflowContext.Deserialize("not json"));
    }
}
