using System.Text.Json.Nodes;
using ChronoCode.Models.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class WorkflowDefinitionSerializerTests
{
    [Fact]
    public void SerializeDeserialize_RoundtripsAllNodeTypes()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare" });
        def.Nodes.Add(new PrepareWorkspaceWorkflowNode { NodeId = "prepare", Name = "Prepare", NextNodeId = "agent" });
        def.Nodes.Add(new AgentWorkflowNode
        {
            NodeId = "agent", Name = "Agent", Backend = WorkflowBackend.Pi,
            PromptTemplate = "do work", NextNodeId = "cond",
            DataContract = new WorkflowDataContract { Fields = [new() { Name = "summary", Type = WorkflowDataType.String, Required = true }] },
            FailurePolicy = new WorkflowNodeFailurePolicy { MaxAttempts = 3, RetryDelaySeconds = 5, ResumeSession = true, RetryOn = [WorkflowRetryReason.LlmApiError] }
        });
        def.Nodes.Add(new ConditionWorkflowNode
        {
            NodeId = "cond", Name = "Cond",
            Predicate = new ComparisonWorkflowPredicate { Path = "$.nodes.agent.output.passed", Operator = WorkflowComparisonOperator.Truthy },
            TrueNodeId = "par", FalseNodeId = "gate"
        });
        def.Nodes.Add(new ParallelWorkflowNode
        {
            NodeId = "par", Name = "Par",
            BranchStartNodeIds = ["b1", "b2"],
            JoinMode = WorkflowParallelJoinMode.AllSucceeded,
            NextNodeId = "fe"
        });
        def.Nodes.Add(new AgentWorkflowNode { NodeId = "b1", Name = "B1", Backend = WorkflowBackend.Pi, DataContract = new(), NextNodeId = "fe" });
        def.Nodes.Add(new AgentWorkflowNode { NodeId = "b2", Name = "B2", Backend = WorkflowBackend.Pi, DataContract = new(), NextNodeId = "fe" });
        def.Nodes.Add(new ForEachWorkflowNode
        {
            NodeId = "fe", Name = "FE", CollectionPath = "$.inputs.items", ItemVariable = "item",
            BodyStartNodeId = "wh", NextNodeId = "commit", MaxIterations = 10
        });
        def.Nodes.Add(new WhileWorkflowNode
        {
            NodeId = "wh", Name = "WH",
            Predicate = new ConstantWorkflowPredicate { Value = false },
            BodyStartNodeId = "agent2", NextNodeId = "fe", MaxIterations = 5
        });
        def.Nodes.Add(new AgentWorkflowNode { NodeId = "agent2", Name = "A2", Backend = WorkflowBackend.Pi, DataContract = new(), NextNodeId = "wh" });
        def.Nodes.Add(new ApprovalGateWorkflowNode { NodeId = "gate", Name = "Gate", Message = "approve", NextNodeId = "commit" });
        def.Nodes.Add(new CommitChangesWorkflowNode { NodeId = "commit", Name = "Commit", CommitMessageTemplate = "AI: {{$.task.name}}", NextNodeId = "pr" });
        def.Nodes.Add(new CreatePullRequestWorkflowNode { NodeId = "pr", Name = "PR", TitleTemplate = "PR", BodyTemplate = "body", NextNodeId = "end" });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end", Name = "End", ResultPath = "$.nodes.pr.output.prUrl" });

        var json = WorkflowDefinitionSerializer.Serialize(def);
        var round = WorkflowDefinitionSerializer.Deserialize(json);

        Assert.NotNull(round);
        Assert.Equal(1, round!.Version);
        Assert.Equal("start", round.StartNodeId);
        Assert.Equal(def.Nodes.Count, round.Nodes.Count);

        // Verify each node type roundtrips
        Assert.IsType<StartWorkflowNode>(round.Nodes[0]);
        Assert.IsType<PrepareWorkspaceWorkflowNode>(round.Nodes[1]);
        var agent = Assert.IsType<AgentWorkflowNode>(round.Nodes[2]);
        Assert.Equal(WorkflowBackend.Pi, agent.Backend);
        Assert.Equal("do work", agent.PromptTemplate);
        Assert.NotNull(agent.DataContract);
        Assert.Equal(1, agent.DataContract!.Fields.Count);
        Assert.NotNull(agent.FailurePolicy);
        Assert.Equal(3, agent.FailurePolicy!.MaxAttempts);

        var cond = Assert.IsType<ConditionWorkflowNode>(round.Nodes[3]);
        Assert.IsType<ComparisonWorkflowPredicate>(cond.Predicate);
        Assert.Equal("par", cond.TrueNodeId);
        Assert.Equal("gate", cond.FalseNodeId);

        var par = Assert.IsType<ParallelWorkflowNode>(round.Nodes[4]);
        Assert.Equal(2, par.BranchStartNodeIds.Count);
        Assert.Equal(WorkflowParallelJoinMode.AllSucceeded, par.JoinMode);

        var fe = Assert.IsType<ForEachWorkflowNode>(round.Nodes[7]);
        Assert.Equal("$.inputs.items", fe.CollectionPath);
        Assert.Equal("item", fe.ItemVariable);
        Assert.Equal(10, fe.MaxIterations);

        var wh = Assert.IsType<WhileWorkflowNode>(round.Nodes[8]);
        Assert.IsType<ConstantWorkflowPredicate>(wh.Predicate);
        Assert.Equal(5, wh.MaxIterations);

        Assert.IsType<ApprovalGateWorkflowNode>(round.Nodes[10]);
        var commit = Assert.IsType<CommitChangesWorkflowNode>(round.Nodes[11]);
        Assert.Equal("AI: {{$.task.name}}", commit.CommitMessageTemplate);

        var pr = Assert.IsType<CreatePullRequestWorkflowNode>(round.Nodes[12]);
        Assert.Equal("PR", pr.TitleTemplate);

        var end = Assert.IsType<EndWorkflowNode>(round.Nodes[13]);
        Assert.Equal("$.nodes.pr.output.prUrl", end.ResultPath);
    }

    [Fact]
    public void Deserialize_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(WorkflowDefinitionSerializer.Deserialize(null));
        Assert.Null(WorkflowDefinitionSerializer.Deserialize(""));
        Assert.Null(WorkflowDefinitionSerializer.Deserialize("   "));
    }

    [Fact]
    public void Serialize_ProdusCamelCaseJson()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", Name = "S", NextNodeId = "end" });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end", Name = "E" });

        var json = WorkflowDefinitionSerializer.Serialize(def);

        Assert.Contains("\"startNodeId\"", json);
        Assert.Contains("\"nextNodeId\"", json);
        Assert.DoesNotContain("\"StartNodeId\"", json);
        Assert.DoesNotContain("\"NextNodeId\"", json);
    }

    [Fact]
    public void SerializePretty_ProdusIndentedJson()
    {
        var def = new WorkflowDefinition { Version = 1, StartNodeId = "start" };
        def.Nodes.Add(new StartWorkflowNode { NodeId = "start", Name = "S", NextNodeId = "end" });
        def.Nodes.Add(new EndWorkflowNode { NodeId = "end", Name = "E" });

        var json = WorkflowDefinitionSerializer.SerializePretty(def);

        Assert.Contains("\n", json);
    }

    [Fact]
    public void SerializeFailurePolicy_Roundtrips()
    {
        var policy = new WorkflowNodeFailurePolicy
        {
            MaxAttempts = 5,
            RetryDelaySeconds = 10,
            ResumeSession = true,
            RetryOn = [WorkflowRetryReason.LlmApiError, WorkflowRetryReason.TransportError, WorkflowRetryReason.Timeout]
        };

        var json = WorkflowDefinitionSerializer.SerializeFailurePolicy(policy);
        var round = WorkflowDefinitionSerializer.DeserializeFailurePolicy(json);

        Assert.NotNull(round);
        Assert.Equal(5, round!.MaxAttempts);
        Assert.Equal(10, round.RetryDelaySeconds);
        Assert.True(round.ResumeSession);
        Assert.Equal(3, round.RetryOn.Count);
    }

    [Fact]
    public void SerializeFailurePolicy_Null_ReturnsEmptyBraces()
    {
        Assert.Equal("{}", WorkflowDefinitionSerializer.SerializeFailurePolicy(null));
    }

    [Fact]
    public void ParseJsonNode_ValidJson_ReturnsNode()
    {
        var node = WorkflowDefinitionSerializer.ParseJsonNode("""{"key":"value"}""");
        Assert.NotNull(node);
        var obj = Assert.IsType<JsonObject>(node);
        Assert.Equal("value", obj["key"]?.GetValue<string>());
    }

    [Fact]
    public void ParseJsonNode_InvalidJson_ReturnsNull()
    {
        Assert.Null(WorkflowDefinitionSerializer.ParseJsonNode("not json"));
        Assert.Null(WorkflowDefinitionSerializer.ParseJsonNode(null));
        Assert.Null(WorkflowDefinitionSerializer.ParseJsonNode(""));
    }
}
