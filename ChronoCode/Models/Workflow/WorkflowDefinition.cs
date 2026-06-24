using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ChronoCode.Models.Workflow;

public sealed class WorkflowDefinition
{
    public int Version { get; set; } = 1;

    public string StartNodeId { get; set; } = string.Empty;

    public List<WorkflowNode> Nodes { get; set; } = [];
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(StartWorkflowNode), typeDiscriminator: "start")]
[JsonDerivedType(typeof(PrepareWorkspaceWorkflowNode), typeDiscriminator: "prepare_workspace")]
[JsonDerivedType(typeof(AgentWorkflowNode), typeDiscriminator: "agent")]
[JsonDerivedType(typeof(ParallelWorkflowNode), typeDiscriminator: "parallel")]
[JsonDerivedType(typeof(ConditionWorkflowNode), typeDiscriminator: "condition")]
[JsonDerivedType(typeof(ForEachWorkflowNode), typeDiscriminator: "for_each")]
[JsonDerivedType(typeof(WhileWorkflowNode), typeDiscriminator: "while")]
[JsonDerivedType(typeof(ApprovalGateWorkflowNode), typeDiscriminator: "approval_gate")]
[JsonDerivedType(typeof(CommitChangesWorkflowNode), typeDiscriminator: "commit_changes")]
[JsonDerivedType(typeof(CreatePullRequestWorkflowNode), typeDiscriminator: "create_pull_request")]
[JsonDerivedType(typeof(EndWorkflowNode), typeDiscriminator: "end")]
public abstract class WorkflowNode
{
    public string NodeId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public abstract class LinearWorkflowNode : WorkflowNode
{
    public string NextNodeId { get; set; } = string.Empty;
}

public sealed class StartWorkflowNode : LinearWorkflowNode;

public sealed class PrepareWorkspaceWorkflowNode : LinearWorkflowNode;

public sealed class AgentWorkflowNode : LinearWorkflowNode
{
    public string PromptTemplate { get; set; } = string.Empty;

    public string? Backend { get; set; }
    public WorkflowDataContract DataContract { get; set; } = new();

    public WorkflowNodeFailurePolicy? FailurePolicy { get; set; }
}

public sealed class ApprovalGateWorkflowNode : LinearWorkflowNode
{
    public string Message { get; set; } = "Manual approval required.";
}

public sealed class CommitChangesWorkflowNode : LinearWorkflowNode
{
    public string CommitMessageTemplate { get; set; } = "AI: {{$.task.name}}";
}

public sealed class CreatePullRequestWorkflowNode : LinearWorkflowNode
{
    public string TitleTemplate { get; set; } = "{{$.task.name}}";

    public string BodyTemplate { get; set; } = "{{$.nodes.execute.output.summary}}";
}

public sealed class ParallelWorkflowNode : WorkflowNode
{
    public List<string> BranchStartNodeIds { get; set; } = [];

    public WorkflowParallelJoinMode JoinMode { get; set; } = WorkflowParallelJoinMode.AllSucceeded;

    public string NextNodeId { get; set; } = string.Empty;
}

public sealed class ConditionWorkflowNode : WorkflowNode
{
    public WorkflowPredicate Predicate { get; set; } = new ConstantWorkflowPredicate();

    public string TrueNodeId { get; set; } = string.Empty;

    public string FalseNodeId { get; set; } = string.Empty;
}

public sealed class ForEachWorkflowNode : WorkflowNode
{
    public string CollectionPath { get; set; } = string.Empty;

    public string ItemVariable { get; set; } = "item";

    public string BodyStartNodeId { get; set; } = string.Empty;

    public string NextNodeId { get; set; } = string.Empty;

    public int MaxIterations { get; set; } = 1;
}

public sealed class WhileWorkflowNode : WorkflowNode
{
    public WorkflowPredicate Predicate { get; set; } = new ConstantWorkflowPredicate();

    public string BodyStartNodeId { get; set; } = string.Empty;

    public string NextNodeId { get; set; } = string.Empty;

    public int MaxIterations { get; set; } = 1;
}

public sealed class EndWorkflowNode : WorkflowNode
{
    public string? ResultPath { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConstantWorkflowPredicate), typeDiscriminator: "constant")]
[JsonDerivedType(typeof(ComparisonWorkflowPredicate), typeDiscriminator: "comparison")]
public abstract class WorkflowPredicate;

public sealed class ConstantWorkflowPredicate : WorkflowPredicate
{
    public bool Value { get; set; }
}

public sealed class ComparisonWorkflowPredicate : WorkflowPredicate
{
    public string Path { get; set; } = string.Empty;

    public WorkflowComparisonOperator Operator { get; set; } = WorkflowComparisonOperator.Equals;

    public JsonNode? Value { get; set; }

    public string? CompareToPath { get; set; }
}

public sealed class WorkflowDataContract
{
    public List<WorkflowDataFieldContract> Fields { get; set; } = [];
}

public sealed class WorkflowDataFieldContract
{
    public string Name { get; set; } = string.Empty;

    public WorkflowDataType Type { get; set; } = WorkflowDataType.String;

    public bool Required { get; set; }
}

public sealed class WorkflowNodeFailurePolicy
{
    public List<WorkflowRetryReason> RetryOn { get; set; } =
    [
        WorkflowRetryReason.LlmApiError,
        WorkflowRetryReason.TransportError,
        WorkflowRetryReason.Timeout
    ];

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 5;

    public bool ResumeSession { get; set; } = true;
}

public enum WorkflowParallelJoinMode
{
    AllSucceeded,
    AllCompleted
}

public enum WorkflowComparisonOperator
{
    Equals,
    NotEquals,
    Truthy,
    Falsy,
    Exists,
    NotExists
}

public enum WorkflowDataType
{
    String,
    Number,
    Boolean,
    Object,
    Array
}

public enum WorkflowRetryReason
{
    LlmApiError,
    TransportError,
    Timeout
}
