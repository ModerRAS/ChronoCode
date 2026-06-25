namespace ChronoCode.Models.Workflow;

public static class WorkflowDefinitionFactory
{
    public const int CurrentVersion = 1;

    public static WorkflowDefinition CreateDefault(bool requirePlanReview, string? legacyPrompt)
    {
        var planPrompt = string.IsNullOrWhiteSpace(legacyPrompt)
            ? "Inspect the repository and produce a concrete plan for the requested task."
            : $"Inspect the repository and produce a concrete plan for:\n{legacyPrompt}";

        var executePrompt = string.IsNullOrWhiteSpace(legacyPrompt)
            ? "Implement the requested changes completely."
            : $"Implement the requested changes completely:\n{legacyPrompt}";

        var plan = new AgentWorkflowNode
        {
            NodeId = "plan",
            Name = "Plan",
            PromptTemplate = planPrompt,
            Backend = WorkflowBackend.Pi,
            DataContract = new WorkflowDataContract
            {
                Fields =
                [
                    new() { Name = "plan", Type = WorkflowDataType.String, Required = true }
                ]
            },
            FailurePolicy = DefaultPiFailurePolicy()
        };

        var definition = new WorkflowDefinition
        {
            Version = CurrentVersion,
            StartNodeId = "start",
            Nodes =
            [
                new StartWorkflowNode { NodeId = "start", Name = "Start", NextNodeId = "prepare_workspace" },
                new PrepareWorkspaceWorkflowNode { NodeId = "prepare_workspace", Name = "Prepare Workspace", NextNodeId = "plan" },
                plan
            ]
        };

        WorkflowNode previous = plan;
        if (requirePlanReview)
        {
            var gate = new ApprovalGateWorkflowNode
            {
                NodeId = "review",
                Name = "Plan Review",
                Message = "Approve the plan before execution.",
                NextNodeId = "execute"
            };
            plan.NextNodeId = "review";
            previous = gate;
            definition.Nodes.Add(gate);
        }

        var execute = new AgentWorkflowNode
        {
            NodeId = "execute",
            Name = "Execute",
            PromptTemplate = executePrompt,
            Backend = WorkflowBackend.Pi,
            DataContract = new WorkflowDataContract
            {
                Fields =
                [
                    new() { Name = "summary", Type = WorkflowDataType.String, Required = true }
                ]
            },
            FailurePolicy = DefaultPiFailurePolicy()
        };
        definition.Nodes.Add(execute);
        ((LinearWorkflowNode)previous).NextNodeId = "execute";
        var commit = new CommitChangesWorkflowNode
        {
            NodeId = "commit",
            Name = "Commit Changes",
            CommitMessageTemplate = "AI: {{$.task.name}}",
            NextNodeId = "pr"
        };
        execute.NextNodeId = "commit";
        definition.Nodes.Add(commit);

        var pr = new CreatePullRequestWorkflowNode
        {
            NodeId = "pr",
            Name = "Create Pull Request",
            TitleTemplate = "{{$.task.name}}",
            BodyTemplate = "{{$.nodes.execute.output.summary}}",
            NextNodeId = "end"
        };
        commit.NextNodeId = "pr";
        definition.Nodes.Add(pr);

        definition.Nodes.Add(new EndWorkflowNode { NodeId = "end", Name = "End" });

        return definition;
    }

    public static string CreateDefaultJson(bool requirePlanReview, string? legacyPrompt) =>
        WorkflowDefinitionSerializer.Serialize(CreateDefault(requirePlanReview, legacyPrompt));

    public static WorkflowNodeFailurePolicy DefaultPiFailurePolicy() => new()
    {
        RetryOn = [WorkflowRetryReason.LlmApiError, WorkflowRetryReason.TransportError, WorkflowRetryReason.Timeout],
        MaxAttempts = 3,
        RetryDelaySeconds = 5,
        ResumeSession = true
    };

    public static string DefaultPiFailurePolicyJson() =>
        WorkflowDefinitionSerializer.SerializeFailurePolicy(DefaultPiFailurePolicy());
}
