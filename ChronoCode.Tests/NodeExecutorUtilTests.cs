using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using ChronoCode.Services.Workflow;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Tests for WorkflowNodeExecutorDispatcher.GetNodeType, WorkflowDefinitionFactory
/// edge cases, and WorkflowDefinitionSerializer round-trip scenarios.
/// </summary>
public class NodeExecutorUtilTests
{
    // ---- GetNodeType ----

    [Fact]
    public void GetNodeType_Start_ReturnsStart()
    {
        Assert.Equal("start", WorkflowNodeExecutorDispatcher.GetNodeType(new StartWorkflowNode { NodeId = "s", Name = "S" }));
    }

    [Fact]
    public void GetNodeType_End_ReturnsEnd()
    {
        Assert.Equal("end", WorkflowNodeExecutorDispatcher.GetNodeType(new EndWorkflowNode { NodeId = "e", Name = "E" }));
    }

    [Fact]
    public void GetNodeType_Agent_ReturnsAgent()
    {
        Assert.Equal("agent", WorkflowNodeExecutorDispatcher.GetNodeType(new AgentWorkflowNode { NodeId = "a", Name = "A" }));
    }

    [Fact]
    public void GetNodeType_Condition_ReturnsCondition()
    {
        Assert.Equal("condition", WorkflowNodeExecutorDispatcher.GetNodeType(new ConditionWorkflowNode { NodeId = "c", Name = "C" }));
    }

    [Fact]
    public void GetNodeType_ForEach_ReturnsForEach()
    {
        Assert.Equal("for_each", WorkflowNodeExecutorDispatcher.GetNodeType(new ForEachWorkflowNode { NodeId = "f", Name = "F" }));
    }

    [Fact]
    public void GetNodeType_While_ReturnsWhile()
    {
        Assert.Equal("while", WorkflowNodeExecutorDispatcher.GetNodeType(new WhileWorkflowNode { NodeId = "w", Name = "W" }));
    }

    [Fact]
    public void GetNodeType_Parallel_ReturnsParallel()
    {
        Assert.Equal("parallel", WorkflowNodeExecutorDispatcher.GetNodeType(new ParallelWorkflowNode { NodeId = "p", Name = "P" }));
    }

    [Fact]
    public void GetNodeType_PrepareWorkspace_ReturnsPrepareWorkspace()
    {
        Assert.Equal("prepare_workspace", WorkflowNodeExecutorDispatcher.GetNodeType(new PrepareWorkspaceWorkflowNode { NodeId = "pw", Name = "PW" }));
    }

    [Fact]
    public void GetNodeType_ApprovalGate_ReturnsApprovalGate()
    {
        Assert.Equal("approval_gate", WorkflowNodeExecutorDispatcher.GetNodeType(new ApprovalGateWorkflowNode { NodeId = "ag", Name = "AG" }));
    }

    [Fact]
    public void GetNodeType_CommitChanges_ReturnsCommitChanges()
    {
        Assert.Equal("commit_changes", WorkflowNodeExecutorDispatcher.GetNodeType(new CommitChangesWorkflowNode { NodeId = "cc", Name = "CC" }));
    }

    [Fact]
    public void GetNodeType_CreatePullRequest_ReturnsCreatePullRequest()
    {
        Assert.Equal("create_pull_request", WorkflowNodeExecutorDispatcher.GetNodeType(new CreatePullRequestWorkflowNode { NodeId = "pr", Name = "PR" }));
    }

    // ---- WorkflowDefinitionFactory edge cases ----

    [Fact]
    public void DefaultGraph_WithReview_HasApprovalGate()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: true, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is ApprovalGateWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_WithoutReview_HasNoApprovalGate()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.DoesNotContain(def.Nodes, n => n is ApprovalGateWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_StartNodeExists()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        var start = def.Nodes.FirstOrDefault(n => n is StartWorkflowNode);
        Assert.NotNull(start);
        Assert.Equal(def.StartNodeId, start!.NodeId);
    }

    [Fact]
    public void DefaultGraph_EndNodeExists()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is EndWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_HasAgentNode()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is AgentWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_HasPrepareWorkspace()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is PrepareWorkspaceWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_HasCommitChanges()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is CommitChangesWorkflowNode);
    }

    [Fact]
    public void DefaultGraph_HasCreatePullRequest()
    {
        var def = WorkflowDefinitionFactory.CreateDefault(requirePlanReview: false, legacyPrompt: null);
        Assert.Contains(def.Nodes, n => n is CreatePullRequestWorkflowNode);
    }

    // ---- WorkflowDefinitionSerializer round-trip ----

    [Fact]
    public void SerializeDeserialize_AllNodeTypes_RoundTrip()
    {
        var original = new WorkflowDefinition
        {
            Version = 1,
            StartNodeId = "start",
            Nodes =
            [
                new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" },
                new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "agent" },
                new AgentWorkflowNode { NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi, PromptTemplate = "do work", DataContract = new(), NextNodeId = "cond" },
                new ConditionWorkflowNode
                {
                    NodeId = "cond", Name = "Cond",
                    Predicate = new ComparisonWorkflowPredicate { Path = "$.nodes.agent.output.passed", Operator = WorkflowComparisonOperator.Truthy },
                    TrueNodeId = "commit", FalseNodeId = "end"
                },
                new CommitChangesWorkflowNode { NodeId = "commit", Name = "Commit", NextNodeId = "pr" },
                new CreatePullRequestWorkflowNode { NodeId = "pr", Name = "PR", NextNodeId = "end" },
                new EndWorkflowNode { NodeId = "end", Name = "End" }
            ]
        };

        var json = WorkflowDefinitionSerializer.Serialize(original);
        var restored = WorkflowDefinitionSerializer.Deserialize(json);

        Assert.Equal(original.StartNodeId, restored.StartNodeId);
        Assert.Equal(original.Nodes.Count, restored.Nodes.Count);
        Assert.Contains(restored.Nodes, n => n is StartWorkflowNode);
        Assert.Contains(restored.Nodes, n => n is AgentWorkflowNode);
        Assert.Contains(restored.Nodes, n => n is ConditionWorkflowNode);
        Assert.Contains(restored.Nodes, n => n is CommitChangesWorkflowNode);
        Assert.Contains(restored.Nodes, n => n is CreatePullRequestWorkflowNode);
        Assert.Contains(restored.Nodes, n => n is EndWorkflowNode);
    }

    [Fact]
    public void SerializeDeserialize_ParallelAndForEach_RoundTrip()
    {
        var original = new WorkflowDefinition
        {
            Version = 1,
            StartNodeId = "start",
            Nodes =
            [
                new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "par" },
                new ParallelWorkflowNode
                {
                    NodeId = "par", Name = "Par",
                    BranchStartNodeIds = ["b1", "b2"],
                    JoinMode = WorkflowParallelJoinMode.AllCompleted,
                    NextNodeId = "loop"
                },
                new AgentWorkflowNode { NodeId = "b1", Name = "B1", PromptTemplate = "x", NextNodeId = "loop" },
                new AgentWorkflowNode { NodeId = "b2", Name = "B2", PromptTemplate = "y", NextNodeId = "loop" },
                new ForEachWorkflowNode
                {
                    NodeId = "loop", Name = "Loop",
                    CollectionPath = "$.inputs.items",
                    BodyStartNodeId = "body",
                    NextNodeId = "end",
                    MaxIterations = 5
                },
                new AgentWorkflowNode { NodeId = "body", Name = "Body", PromptTemplate = "proc", NextNodeId = "end" },
                new EndWorkflowNode { NodeId = "end", Name = "End" }
            ]
        };

        var json = WorkflowDefinitionSerializer.Serialize(original);
        var restored = WorkflowDefinitionSerializer.Deserialize(json);

        Assert.Equal(7, restored.Nodes.Count);
        var par = restored.Nodes.OfType<ParallelWorkflowNode>().FirstOrDefault();
        Assert.NotNull(par);
        Assert.Equal(2, par!.BranchStartNodeIds.Count);
        Assert.Equal(WorkflowParallelJoinMode.AllCompleted, par.JoinMode);

        var loop = restored.Nodes.OfType<ForEachWorkflowNode>().FirstOrDefault();
        Assert.NotNull(loop);
        Assert.Equal(5, loop!.MaxIterations);
        Assert.Equal("$.inputs.items", loop.CollectionPath);
    }

    [Fact]
    public void SerializeDeserialize_WhileLoop_RoundTrip()
    {
        var original = new WorkflowDefinition
        {
            Version = 1,
            StartNodeId = "start",
            Nodes =
            [
                new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "wh" },
                new WhileWorkflowNode
                {
                    NodeId = "wh", Name = "While",
                    Predicate = new ComparisonWorkflowPredicate { Path = "$.inputs.continue", Operator = WorkflowComparisonOperator.Truthy },
                    BodyStartNodeId = "body",
                    NextNodeId = "end",
                    MaxIterations = 3
                },
                new AgentWorkflowNode { NodeId = "body", Name = "Body", PromptTemplate = "loop", NextNodeId = "end" },
                new EndWorkflowNode { NodeId = "end", Name = "End" }
            ]
        };

        var json = WorkflowDefinitionSerializer.Serialize(original);
        var restored = WorkflowDefinitionSerializer.Deserialize(json);

        var wh = restored.Nodes.OfType<WhileWorkflowNode>().FirstOrDefault();
        Assert.NotNull(wh);
        Assert.Equal(3, wh!.MaxIterations);
    }

    [Fact]
    public void SerializeDeserialize_ApprovalGate_RoundTrip()
    {
        var original = new WorkflowDefinition
        {
            Version = 1,
            StartNodeId = "start",
            Nodes =
            [
                new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "ag" },
                new ApprovalGateWorkflowNode { NodeId = "ag", Name = "Gate", Message = "Please approve", NextNodeId = "end" },
                new EndWorkflowNode { NodeId = "end", Name = "End" }
            ]
        };

        var json = WorkflowDefinitionSerializer.Serialize(original);
        var restored = WorkflowDefinitionSerializer.Deserialize(json);

        var ag = restored.Nodes.OfType<ApprovalGateWorkflowNode>().FirstOrDefault();
        Assert.NotNull(ag);
        Assert.Equal("Please approve", ag!.Message);
    }

    [Fact]
    public void Serialize_EmptyWorkflow_ProducesValidJson()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start", Nodes = [] };
        var json = WorkflowDefinitionSerializer.Serialize(def);
        Assert.NotNull(json);
        Assert.Contains("\"version\"", json);
    }
}
