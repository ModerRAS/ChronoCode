using ChronoCode.Models.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class WorkflowDefinitionFactoryTests
{
    [Fact]
    public void DefaultGraph_NoReview_HasExpectedShape()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: "do thing");

        Assert.Equal(1, def.Version);
        Assert.Equal("start", def.StartNodeId);

        var plan = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "plan");
        Assert.Contains("do thing", plan.PromptTemplate);
        Assert.Equal(WorkflowBackend.Pi, plan.Backend);
        Assert.NotNull(plan.DataContract);
        Assert.NotNull(plan.FailurePolicy);

        var nodeIds = def.Nodes.Select(n => n.NodeId).ToList();
        Assert.Equal(new[] { "start", "prepare_workspace", "plan", "execute", "commit", "pr", "end" }, nodeIds);

        Assert.DoesNotContain("review", nodeIds);
    }

    [Fact]
    public void DefaultGraph_WithReview_InsertsApprovalGate()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: true, legacyPrompt: null);

        var nodeIds = def.Nodes.Select(n => n.NodeId).ToList();
        Assert.Equal(new[] { "start", "prepare_workspace", "plan", "review", "execute", "commit", "pr", "end" }, nodeIds);

        var plan = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "plan");
        Assert.Equal("review", plan.NextNodeId);

        var gate = def.Nodes.OfType<ApprovalGateWorkflowNode>().Single();
        Assert.Equal("execute", gate.NextNodeId);
    }

    [Fact]
    public void DefaultGraph_WiresLinearChainToEnd()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var start = def.Nodes.OfType<StartWorkflowNode>().Single();
        var prepare = def.Nodes.OfType<PrepareWorkspaceWorkflowNode>().Single();
        var plan = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "plan");
        var execute = def.Nodes.OfType<AgentWorkflowNode>().Single(a => a.NodeId == "execute");
        var commit = def.Nodes.OfType<CommitChangesWorkflowNode>().Single();
        var pr = def.Nodes.OfType<CreatePullRequestWorkflowNode>().Single();
        var end = def.Nodes.OfType<EndWorkflowNode>().Single();

        Assert.Equal("prepare_workspace", start.NextNodeId);
        Assert.Equal("plan", prepare.NextNodeId);
        Assert.Equal("execute", plan.NextNodeId);
        Assert.Equal("commit", execute.NextNodeId);
        Assert.Equal("pr", commit.NextNodeId);
        Assert.Equal("end", pr.NextNodeId);
        Assert.Null(end.ResultPath);
    }

    [Fact]
    public void DefaultGraph_IsValid()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(true, "legacy prompt");
        Assert.True(WorkflowDefinitionValidator.IsValid(def, out var error), error);
    }

    [Fact]
    public void DefaultPiFailurePolicy_MatchesPlanDefaults()
    {
        var policy = WorkflowDefinitionFactory.DefaultPiFailurePolicy();
        Assert.Equal(3, policy.MaxAttempts);
        Assert.Equal(5, policy.RetryDelaySeconds);
        Assert.True(policy.ResumeSession);
        Assert.Contains(WorkflowRetryReason.LlmApiError, policy.RetryOn);
        Assert.Contains(WorkflowRetryReason.TransportError, policy.RetryOn);
        Assert.Contains(WorkflowRetryReason.Timeout, policy.RetryOn);
    }

    [Fact]
    public void JsonRoundtrip_PreservesShape()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, "x");
        var json = WorkflowDefinitionSerializer.Serialize(def);
        var round = WorkflowDefinitionSerializer.Deserialize(json);

        Assert.NotNull(round);
        Assert.Equal(def.StartNodeId, round!.StartNodeId);
        Assert.Equal(def.Nodes.Count, round.Nodes.Count);
        Assert.Contains(round.Nodes, n => n is AgentWorkflowNode a && a.NodeId == "plan");
    }
}
