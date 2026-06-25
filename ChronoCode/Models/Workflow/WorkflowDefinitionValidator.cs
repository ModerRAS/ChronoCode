using System.Text.Json;

namespace ChronoCode.Models.Workflow;

public static class WorkflowDefinitionValidator
{
    public static bool IsValid(string? workflowDefinitionJson, out string error)
    {
        var definition = WorkflowDefinitionSerializer.Deserialize(workflowDefinitionJson);
        if (definition == null)
        {
            error = "WorkflowDefinitionJson is not valid JSON.";
            return false;
        }

        return IsValid(definition, out error);
    }

    public static bool IsValid(WorkflowDefinition definition, out string error)
    {
        if (definition.Nodes == null || definition.Nodes.Count == 0)
        {
            error = "Workflow must contain at least one node.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.StartNodeId))
        {
            error = "startNodeId is required.";
            return false;
        }

        var byId = new Dictionary<string, WorkflowNode>(StringComparer.Ordinal);
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.NodeId))
            {
                error = "Every node must have a non-empty nodeId.";
                return false;
            }

            if (!byId.TryAdd(node.NodeId, node))
            {
                error = $"Duplicate nodeId: {node.NodeId}.";
                return false;
            }
        }

        if (!byId.ContainsKey(definition.StartNodeId))
        {
            error = $"startNodeId '{definition.StartNodeId}' does not reference a node.";
            return false;
        }

        if (byId[definition.StartNodeId] is not StartWorkflowNode)
        {
            error = "startNodeId must reference a 'start' node.";
            return false;
        }

        var hasEnd = false;
        foreach (var node in definition.Nodes)
        {
            switch (node)
            {
                case StartWorkflowNode s:
                    RejectSelfOrMissing(s.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case PrepareWorkspaceWorkflowNode p:
                    RejectSelfOrMissing(p.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case ApprovalGateWorkflowNode a:
                    RejectSelfOrMissing(a.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case CommitChangesWorkflowNode c:
                    RejectSelfOrMissing(c.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case CreatePullRequestWorkflowNode pr:
                    RejectSelfOrMissing(pr.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case AgentWorkflowNode ag:
                    if (ag.DataContract == null)
                    {
                        error = $"agent node '{node.NodeId}' must declare a dataContract.";
                        return false;
                    }
                    RejectSelfOrMissing(ag.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    if (!string.IsNullOrWhiteSpace(ag.Backend) &&
                        !string.Equals(ag.Backend, WorkflowBackend.Pi, StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"agent node '{node.NodeId}' backend must be 'pi' (opencode is not allowed for workflow agent nodes).";
                        return false;
                    }
                    break;
                case ConditionWorkflowNode cond:
                    RejectMissing(cond.TrueNodeId, byId, node.NodeId, "trueNodeId", out error);
                    if (error != null) return false;
                    RejectMissing(cond.FalseNodeId, byId, node.NodeId, "falseNodeId", out error);
                    if (error != null) return false;
                    break;
                case ParallelWorkflowNode par:
                    if (par.BranchStartNodeIds == null || par.BranchStartNodeIds.Count == 0)
                    {
                        error = $"parallel node '{node.NodeId}' must have a non-empty branchStartNodeIds.";
                        return false;
                    }
                    foreach (var branch in par.BranchStartNodeIds)
                    {
                        RejectMissing(branch, byId, node.NodeId, "branchStartNodeIds", out error);
                        if (error != null) return false;
                    }
                    RejectSelfOrMissing(par.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case ForEachWorkflowNode fe:
                    if (fe.MaxIterations < 1)
                    {
                        error = $"for_each node '{node.NodeId}' maxIterations must be >= 1.";
                        return false;
                    }
                    RejectMissing(fe.BodyStartNodeId, byId, node.NodeId, "bodyStartNodeId", out error);
                    if (error != null) return false;
                    RejectSelfOrMissing(fe.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case WhileWorkflowNode w:
                    if (w.MaxIterations < 1)
                    {
                        error = $"while node '{node.NodeId}' maxIterations must be >= 1.";
                        return false;
                    }
                    RejectMissing(w.BodyStartNodeId, byId, node.NodeId, "bodyStartNodeId", out error);
                    if (error != null) return false;
                    RejectSelfOrMissing(w.NextNodeId, byId, node.NodeId, out error);
                    if (error != null) return false;
                    break;
                case EndWorkflowNode:
                    hasEnd = true;
                    break;
            }
        }

        if (!hasEnd)
        {
            error = "Workflow must contain at least one 'end' node.";
            return false;
        }

        if (!IsAcyclicOutsideCompositeBodies(definition, byId, out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static void RejectSelfOrMissing(string next, Dictionary<string, WorkflowNode> byId, string nodeId, out string error)
    {
        if (string.IsNullOrWhiteSpace(next))
        {
            error = $"Node '{nodeId}' must declare a nextNodeId.";
            return;
        }
        if (next == nodeId)
        {
            error = $"Node '{nodeId}' nextNodeId cannot reference itself.";
            return;
        }
        if (!byId.ContainsKey(next))
        {
            error = $"Node '{nodeId}' references unknown nextNodeId '{next}'.";
            return;
        }
        error = null!;
    }

    private static void RejectMissing(string target, Dictionary<string, WorkflowNode> byId, string nodeId, string field, out string error)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            error = $"Node '{nodeId}' must declare {field}.";
            return;
        }
        if (!byId.ContainsKey(target))
        {
            error = $"Node '{nodeId}' {field} references unknown nodeId '{target}'.";
            return;
        }
        error = null!;
    }

    private static bool IsAcyclicOutsideCompositeBodies(WorkflowDefinition definition, Dictionary<string, WorkflowNode> byId, out string error)
    {
        var compositeBodies = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in definition.Nodes)
        {
            switch (node)
            {
                case ForEachWorkflowNode fe:
                    compositeBodies.Add(fe.BodyStartNodeId);
                    break;
                case WhileWorkflowNode w:
                    compositeBodies.Add(w.BodyStartNodeId);
                    break;
                case ParallelWorkflowNode par:
                    foreach (var b in par.BranchStartNodeIds)
                    {
                        compositeBodies.Add(b);
                    }
                    break;
            }
        }

        var color = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in definition.Nodes)
        {
            if (!color.ContainsKey(node.NodeId))
            {
                if (HasOuterCycle(node.NodeId, byId, color, compositeBodies))
                {
                    error = $"Workflow graph contains a cycle outside of loop/parallel bodies at '{node.NodeId}'.";
                    return false;
                }
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool HasOuterCycle(string nodeId, Dictionary<string, WorkflowNode> byId, Dictionary<string, int> color, HashSet<string> compositeBodies)
    {
        color[nodeId] = 1;
        var neighbors = NextNodes(byId[nodeId]);
        foreach (var next in neighbors)
        {
            if (!byId.TryGetValue(next, out _))
            {
                continue;
            }

            if (compositeBodies.Contains(next))
            {
                continue;
            }

            if (!color.TryGetValue(next, out var c))
            {
                if (HasOuterCycle(next, byId, color, compositeBodies))
                {
                    return true;
                }
            }
            else if (c == 1)
            {
                return true;
            }
        }
        color[nodeId] = 2;
        return false;
    }

    private static List<string> NextNodes(WorkflowNode node)
    {
        return node switch
        {
            StartWorkflowNode s => [s.NextNodeId],
            PrepareWorkspaceWorkflowNode p => [p.NextNodeId],
            AgentWorkflowNode a => [a.NextNodeId],
            ApprovalGateWorkflowNode ap => [ap.NextNodeId],
            CommitChangesWorkflowNode c => [c.NextNodeId],
            CreatePullRequestWorkflowNode pr => [pr.NextNodeId],
            ConditionWorkflowNode cd => [cd.TrueNodeId, cd.FalseNodeId],
            ParallelWorkflowNode pa => [pa.NextNodeId],
            ForEachWorkflowNode fe => [fe.NextNodeId],
            WhileWorkflowNode w => [w.NextNodeId],
            EndWorkflowNode => [],
            _ => []
        };
    }
}
