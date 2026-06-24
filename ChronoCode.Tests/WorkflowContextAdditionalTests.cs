using System.Text.Json.Nodes;
using ChronoCode.Models.Workflow;
using ChronoCode.Services.Workflow;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Additional WorkflowContext tests: GetVariable, ResolvePath edge cases,
/// EvaluatePredicate comparisons, scope key variations.
/// </summary>
public class WorkflowContextAdditionalTests
{
    private static WorkflowContext CreateContext()
    {
        var ctx = new WorkflowContext();
        ctx.Root["task"] = new JsonObject();
        ctx.Root["run"] = new JsonObject();
        ctx.Task["name"] = "test-task";
        ctx.Task["maxFiles"] = 50;
        ctx.Run["workspacePath"] = "/tmp/ws";
        ctx.Run["branchName"] = "feature/test";
        return ctx;
    }

    // ---- GetVariable ----

    [Fact]
    public void GetVariable_ReturnsValue_WhenExists()
    {
        var ctx = CreateContext();
        ctx.SetVariable("myVar", JsonValue.Create(42));

        var result = ctx.GetVariable("myVar");
        Assert.NotNull(result);
        Assert.Equal(42, result!.GetValue<int>());
    }

    [Fact]
    public void GetVariable_ReturnsNull_WhenMissing()
    {
        var ctx = CreateContext();
        Assert.Null(ctx.GetVariable("nonexistent"));
    }

    [Fact]
    public void GetVariable_ReturnsNull_WhenSetToNull()
    {
        var ctx = CreateContext();
        ctx.SetVariable("tempVar", JsonValue.Create("hello"));
        ctx.SetVariable("tempVar", null);

        Assert.Null(ctx.GetVariable("tempVar"));
    }

    // ---- ResolvePath edge cases ----

    [Fact]
    public void ResolvePath_DeeplyNested_ReturnsValue()
    {
        var ctx = CreateContext();
        ctx.Run["nested"] = new JsonObject { ["deep"] = new JsonObject { ["value"] = "found" } };

        var result = ctx.ResolvePath("$.run.nested.deep.value");
        Assert.Equal("found", result?.GetValue<string>());
    }

    [Fact]
    public void ResolvePath_OnNonObject_ReturnsNull()
    {
        var ctx = CreateContext();
        ctx.SetVariable("str", JsonValue.Create("hello"));

        Assert.Null(ctx.ResolvePath("$.str.property"));
    }

    // ---- EvaluatePredicate additional comparisons ----

    [Fact]
    public void EvaluatePredicate_Equals_OnNumberMatchingValue_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("count", JsonValue.Create(10));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.count",
            Operator = WorkflowComparisonOperator.Equals,
            Value = JsonValue.Create(10)
        };

        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Equals_OnNumberNonMatching_ReturnsFalse()
    {
        var ctx = CreateContext();
        ctx.SetVariable("count", JsonValue.Create(3));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.count",
            Operator = WorkflowComparisonOperator.Equals,
            Value = JsonValue.Create(5)
        };

        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Truthy_OnZero_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("val", JsonValue.Create(0));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.val",
            Operator = WorkflowComparisonOperator.Truthy
        };

        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Falsy_OnZero_ReturnsFalse()
    {
        var ctx = CreateContext();
        ctx.SetVariable("val", JsonValue.Create(0));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.val",
            Operator = WorkflowComparisonOperator.Falsy
        };

        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Falsy_OnEmptyString_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("val", JsonValue.Create(""));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.val",
            Operator = WorkflowComparisonOperator.Falsy
        };

        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Falsy_OnNull_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("val", null);

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.val",
            Operator = WorkflowComparisonOperator.Falsy
        };

        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_NotEquals_OnDifferentType_ReturnsTrue()
    {
        var ctx = CreateContext();
        ctx.SetVariable("val", JsonValue.Create("string"));

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.val",
            Operator = WorkflowComparisonOperator.NotEquals,
            Value = JsonValue.Create(42)
        };

        Assert.True(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Equals_OnMissingPath_ReturnsFalse()
    {
        var ctx = CreateContext();

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.nonexistent",
            Operator = WorkflowComparisonOperator.Equals,
            Value = JsonValue.Create(42)
        };

        Assert.False(ctx.EvaluatePredicate(pred));
    }

    [Fact]
    public void EvaluatePredicate_Truthy_OnMissingPath_ReturnsFalse()
    {
        var ctx = CreateContext();

        var pred = new ComparisonWorkflowPredicate
        {
            Path = "$.nonexistent",
            Operator = WorkflowComparisonOperator.Truthy
        };

        Assert.False(ctx.EvaluatePredicate(pred));
    }

    // ---- ComputeScopeKey additional ----

    [Fact]
    public void ComputeScopeKey_SingleForEach_HasIndex()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "for_each", Index = 3 });

        var key = ctx.ComputeScopeKey();

        Assert.Contains("3", key);
    }

    [Fact]
    public void ComputeScopeKey_SingleParallel_HasBranchIndex()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "parallel", BranchIndex = 1 });

        var key = ctx.ComputeScopeKey();

        Assert.Contains("1", key);
    }

    [Fact]
    public void ComputeScopeKey_SingleWhile_HasCount()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "while", Count = 7 });

        var key = ctx.ComputeScopeKey();

        Assert.Contains("7", key);
    }

    [Fact]
    public void ComputeScopeKey_PopFrame_ReducesKey()
    {
        var ctx = CreateContext();
        ctx.Frames.Add(new WorkflowFrame { Type = "for_each", Index = 5 });
        var keyWithFrame = ctx.ComputeScopeKey();

        ctx.Frames.Clear();
        var keyWithoutFrame = ctx.ComputeScopeKey();

        Assert.NotEqual(keyWithFrame, keyWithoutFrame);
    }
}
