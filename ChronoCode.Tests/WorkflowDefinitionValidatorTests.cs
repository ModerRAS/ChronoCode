using ChronoCode.Models.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class WorkflowDefinitionValidatorTests
{
    private static WorkflowDefinition ValidBaseline()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" });
        def.Nodes.Add(new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "agent" });
        def.Nodes.Add(new AgentWorkflowNode
        {
            NodeId = "agent",
            Name = "Agent",
            NextNodeId = "end",
            Backend = WorkflowBackend.Pi,
            PromptTemplate = "do work",
            DataContract = new WorkflowDataContract { Fields = [new() { Name = "summary", Type = WorkflowDataType.String, Required = true }] }
        });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end", Name = "End" });
        return def;
    }

    [Fact]
    public void Valid_BaselineGraph_Passes()
    {
        Assert.True(WorkflowDefinitionValidator.IsValid(ValidBaseline(), out var error), error);
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void Missing_StartNodeId_Fails()
    {
        var def = ValidBaseline();
        def.StartNodeId = "";
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("startNodeId", error);
    }

    [Fact]
    public void Bad_NodeId_Reference_Fails()
    {
        var def = ValidBaseline();
        ((StartWorkflowNode)def.Nodes[0]).NextNodeId = "does-not-exist";
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("unknown nextNodeId", error);
    }

    [Fact]
    public void Empty_Parallel_Branches_Fails()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", NextNodeId = "par" });
        def.Nodes.Add(new ParallelWorkflowNode { NodeId = "par", BranchStartNodeIds = new(), JoinMode = WorkflowParallelJoinMode.AllSucceeded, NextNodeId = "end" });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end" });
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("branchStartNodeIds", error);
    }

    [Fact]
    public void While_MaxIterations_Zero_Fails()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", NextNodeId = "w" });
        def.Nodes.Add(new WhileWorkflowNode { NodeId = "w", BodyStartNodeId = "body", NextNodeId = "end", MaxIterations = 0, Predicate = new ConstantWorkflowPredicate { Value = true } });
        def.Nodes.Add(new AgentWorkflowNode { NodeId = "body", NextNodeId = "end", Backend = WorkflowBackend.Pi, DataContract = new WorkflowDataContract() });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end" });
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("maxIterations", error);
    }

    [Fact]
    public void Agent_Missing_DataContract_Fails()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", NextNodeId = "agent" });
        def.Nodes.Add(new AgentWorkflowNode { NodeId = "agent", NextNodeId = "end", Backend = WorkflowBackend.Pi, DataContract = null! });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end" });
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("dataContract", error);
    }

    [Fact]
    public void Agent_Opencode_Backend_Fails()
    {
        var def = ValidBaseline();
        ((AgentWorkflowNode)def.Nodes[2]).Backend = WorkflowBackend.Opencode;
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("pi", error);
    }

    [Fact]
    public void Missing_End_Fails()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", NextNodeId = "a" });
        def.Nodes.Add(new PrepareWorkspaceWorkflowNode { NodeId = "a", NextNodeId = "start" });
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("end", error);
    }

    [Fact]
    public void Outer_Cycle_Fails()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", NextNodeId = "a" });
        def.Nodes.Add(new PrepareWorkspaceWorkflowNode { NodeId = "a", NextNodeId = "start" });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end" });
        Assert.False(WorkflowDefinitionValidator.IsValid(def, out var error));
        Assert.Contains("cycle", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Json_String_Overload_Validates()
    {
        var json = WorkflowDefinitionSerializer.Serialize(ValidBaseline());
        Assert.True(WorkflowDefinitionValidator.IsValid(json, out var error), error);
    }
}
