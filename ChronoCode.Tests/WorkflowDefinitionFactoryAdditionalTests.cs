using ChronoCode.Models.Workflow;
using Xunit;

namespace ChronoCode.Tests;

/// <summary>
/// Additional WorkflowDefinitionFactory tests: legacy prompt handling,
/// failure policy defaults, JSON serialization, version constant.
/// </summary>
public class WorkflowDefinitionFactoryAdditionalTests
{
    [Fact]
    public void CreateDefault_WithLegacyPrompt_UsesPromptInPlanNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, "Fix the login bug");

        var planNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "plan"));
        Assert.Contains("Fix the login bug", planNode.PromptTemplate);
    }

    [Fact]
    public void CreateDefault_WithLegacyPrompt_UsesPromptInExecuteNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, "Fix the login bug");

        var executeNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "execute"));
        Assert.Contains("Fix the login bug", executeNode.PromptTemplate);
    }

    [Fact]
    public void CreateDefault_WithoutLegacyPrompt_UsesDefaultPlanPrompt()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var planNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "plan"));
        Assert.Contains("Inspect the repository", planNode.PromptTemplate);
    }

    [Fact]
    public void CreateDefault_WithoutLegacyPrompt_UsesDefaultExecutePrompt()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var executeNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "execute"));
        Assert.Contains("Implement the requested changes", executeNode.PromptTemplate);
    }

    [Fact]
    public void CreateDefault_WithEmptyLegacyPrompt_UsesDefaultPrompts()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, "");

        var planNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "plan"));
        Assert.Contains("Inspect the repository", planNode.PromptTemplate);
        Assert.DoesNotContain("\n", planNode.PromptTemplate);
    }

    [Fact]
    public void CreateDefault_WithWhitespaceLegacyPrompt_UsesDefaultPrompts()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, "   ");

        var planNode = Assert.IsType<AgentWorkflowNode>(
            def.Nodes.FirstOrDefault(n => n.NodeId == "plan"));
        Assert.Contains("Inspect the repository", planNode.PromptTemplate);
    }

    [Fact]
    public void DefaultPiFailurePolicy_HasExpectedDefaults()
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
    public void DefaultPiFailurePolicyJson_IsValidJson()
    {
        var json = WorkflowDefinitionFactory.DefaultPiFailurePolicyJson();

        Assert.False(string.IsNullOrWhiteSpace(json));
        // Should be deserializable
        var policy = WorkflowDefinitionSerializer.DeserializeFailurePolicy(json);
        Assert.Equal(3, policy.MaxAttempts);
    }

    [Fact]
    public void CreateDefaultJson_ProducesValidWorkflow()
    {
        var json = WorkflowDefinitionFactory.CreateDefaultJson(false, null);

        Assert.True(WorkflowDefinitionValidator.IsValid(json, out var error), error);
    }

    [Fact]
    public void CreateDefaultJson_WithReview_ProducesValidWorkflow()
    {
        var json = WorkflowDefinitionFactory.CreateDefaultJson(true, "Do work");

        Assert.True(WorkflowDefinitionValidator.IsValid(json, out var error), error);
    }

    [Fact]
    public void CreateDefault_CurrentVersion_MatchesConstant()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        Assert.Equal(WorkflowDefinitionFactory.CurrentVersion, def.Version);
    }

    [Fact]
    public void CreateDefault_AgentNodes_HavePiBackend()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var agentNodes = def.Nodes.OfType<AgentWorkflowNode>();
        Assert.NotEmpty(agentNodes);
        Assert.All(agentNodes, n => Assert.Equal(WorkflowBackend.Pi, n.Backend));
    }

    [Fact]
    public void CreateDefault_AgentNodes_HaveFailurePolicy()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var agentNodes = def.Nodes.OfType<AgentWorkflowNode>();
        Assert.All(agentNodes, n => Assert.NotNull(n.FailurePolicy));
    }

    [Fact]
    public void CreateDefault_AgentNodes_HaveDataContract()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var planNode = def.Nodes.OfType<AgentWorkflowNode>().FirstOrDefault(n => n.NodeId == "plan");
        Assert.NotNull(planNode!.DataContract);
        Assert.NotEmpty(planNode.DataContract.Fields);
    }

    [Fact]
    public void CreateDefault_WithReview_HasApprovalGateNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(true, null);

        var approvalNode = def.Nodes.OfType<ApprovalGateWorkflowNode>().FirstOrDefault();
        Assert.NotNull(approvalNode);
    }

    [Fact]
    public void CreateDefault_WithoutReview_HasNoApprovalGateNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        var approvalNodes = def.Nodes.OfType<ApprovalGateWorkflowNode>();
        Assert.Empty(approvalNodes);
    }

    [Fact]
    public void CreateDefault_HasStartNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        Assert.NotNull(def.Nodes.FirstOrDefault(n => n.NodeId == "start"));
        Assert.Equal("start", def.StartNodeId);
    }

    [Fact]
    public void CreateDefault_HasPrepareWorkspaceNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        Assert.NotNull(def.Nodes.FirstOrDefault(n => n.NodeId == "prepare_workspace"));
    }

    [Fact]
    public void CreateDefault_HasCommitChangesNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        Assert.NotNull(def.Nodes.FirstOrDefault(n => n.NodeId == "commit"));
    }

    [Fact]
    public void CreateDefault_HasCreatePullRequestNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(false, null);

        Assert.NotNull(def.Nodes.FirstOrDefault(n => n.NodeId == "pr"));
    }
}
