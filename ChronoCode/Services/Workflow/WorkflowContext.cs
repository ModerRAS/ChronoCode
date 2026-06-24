using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>
/// Mutable workflow run state: the JSON context root (task/run/inputs/nodes + loop
/// variables) plus the control-flow frame stack. Serializes to/from
/// <see cref="TaskExecution.WorkflowStateJson"/> for resume.
/// </summary>
public sealed class WorkflowContext
{
    private const string TaskKey = "task";
    private const string RunKey = "run";
    private const string InputsKey = "inputs";
    private const string NodesKey = "nodes";

    public JsonObject Root { get; } = new();
    public List<WorkflowFrame> Frames { get; } = [];

    public JsonObject Task => Root[TaskKey]?.AsObject() ?? new JsonObject();
    public JsonObject Run => Root[RunKey]?.AsObject() ?? new JsonObject();
    public JsonObject Inputs => Root[InputsKey]?.AsObject() ?? new JsonObject();
    public JsonObject Nodes => Root[NodesKey]?.AsObject() ?? new JsonObject();

    public void InitFrom(ScheduledTask task, TaskExecution run, string? defaultInputsJson)
    {
        Root[TaskKey] = ToJsonObject(task);
        Root[RunKey] = ToJsonObject(run);
        Root[InputsKey] = string.IsNullOrWhiteSpace(defaultInputsJson)
            ? new JsonObject()
            : (JsonNode.Parse(defaultInputsJson)?.DeepClone() as JsonObject ?? new JsonObject());
        Root[NodesKey] = new JsonObject();
        Frames.Clear();
    }

    private static JsonObject ToJsonObject(object obj)
    {
        var node = JsonSerializer.SerializeToNode(obj, WorkflowDefinitionSerializer.Options);
        return node is JsonObject jo ? jo : new JsonObject();
    }

    public void SetNodeOutput(string nodeId, JsonNode? output)
    {
        var holder = Nodes[nodeId]?.DeepClone() as JsonObject ?? new JsonObject();
        if (output == null)
        {
            holder.Remove("output");
        }
        else
        {
            holder["output"] = output.DeepClone();
        }
        Nodes[nodeId] = holder;
    }

    public JsonNode? GetNodeOutput(string nodeId)
    {
        return Nodes[nodeId]?["output"];
    }

    /// <summary>Sets a top-level loop variable (e.g. for_each item).</summary>
    public void SetVariable(string name, JsonNode? value)
    {
        Root[name] = value?.DeepClone();
    }

    public JsonNode? GetVariable(string name) => Root[name];

    /// <summary>
    /// Resolves a JSON path of the form <c>$.a.b.c</c> or <c>$.a[0].b</c> against the
    /// context root. Returns null if any segment is missing.
    /// </summary>
    public JsonNode? ResolvePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var p = path.Trim();
        if (p == "$")
        {
            return Root;
        }

        if (p.StartsWith("$.")) p = p[2..];
        else if (p.StartsWith("$")) p = p[1..];

        JsonNode? current = Root;
        var i = 0;
        while (i < p.Length && current != null)
        {
            var c = p[i];
            if (c == '.')
            {
                i++;
                continue;
            }

            if (c == '[')
            {
                var close = p.IndexOf(']', i);
                if (close < 0) return null;
                if (!int.TryParse(p[(i + 1)..close], out var idx)) return null;
                current = current is JsonArray arr ? arr[idx] : null;
                i = close + 1;
                continue;
            }

            var j = i;
            while (j < p.Length && p[j] != '.' && p[j] != '[') j++;
            var seg = p[i..j];
            current = current is JsonObject obj && obj.TryGetPropertyValue(seg, out var child) ? child : null;
            i = j;
        }

        return current;
    }

    public bool EvaluatePredicate(WorkflowPredicate? predicate)
    {
        switch (predicate)
        {
            case null:
                return false;
            case ConstantWorkflowPredicate constant:
                return constant.Value;
            case ComparisonWorkflowPredicate cmp:
                return EvaluateComparison(cmp);
            default:
                return false;
        }
    }

    private bool EvaluateComparison(ComparisonWorkflowPredicate cmp)
    {
        var left = ResolvePath(cmp.Path);
        switch (cmp.Operator)
        {
            case WorkflowComparisonOperator.Exists:
                return left != null;
            case WorkflowComparisonOperator.NotExists:
                return left == null;
            case WorkflowComparisonOperator.Truthy:
                return IsTruthy(left);
            case WorkflowComparisonOperator.Falsy:
                return !IsTruthy(left);
            case WorkflowComparisonOperator.Equals:
                {
                    var right = cmp.CompareToPath != null ? ResolvePath(cmp.CompareToPath) : cmp.Value;
                    return JsonNode.DeepEquals(left, right);
                }
            case WorkflowComparisonOperator.NotEquals:
                {
                    var right = cmp.CompareToPath != null ? ResolvePath(cmp.CompareToPath) : cmp.Value;
                    return !JsonNode.DeepEquals(left, right);
                }
            default:
                return false;
        }
    }

    private static bool IsTruthy(JsonNode? node)
    {
        if (node == null) return false;
        if (node is JsonValue v)
        {
            if (v.TryGetValue<bool>(out var b)) return b;
            if (v.TryGetValue<decimal>(out var d)) return d != 0m;
            if (v.TryGetValue<double>(out var dbl)) return dbl != 0d;
            if (v.TryGetValue<long>(out var l)) return l != 0L;
            if (v.TryGetValue<string>(out var s)) return !string.IsNullOrEmpty(s);
        }
        return true;
    }

    /// <summary>
    /// Scope key derived from the current frame stack so the same node id can be
    /// visited multiple times (loops / parallel branches) with distinct node-execution
    /// records.
    /// </summary>
    public string ComputeScopeKey()
    {
        if (Frames.Count == 0) return "root";
        return string.Join("|", Frames.Select(f => f.Type switch
        {
            "for_each" => $"{f.NodeId}#{f.Index}",
            "parallel" => $"{f.NodeId}#{f.BranchIndex}",
            "while" => $"{f.NodeId}#{f.Count}",
            _ => f.NodeId
        }));
    }

    public string Serialize()
    {
        var obj = new JsonObject
        {
            ["root"] = Root.DeepClone(),
            ["frames"] = new JsonArray(Frames.Select(SerializeFrame).ToArray())
        };
        return obj.ToJsonString();
    }

    public static WorkflowContext? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is not JsonObject obj) return null;
            var ctx = new WorkflowContext();
            if (obj["root"] is JsonObject root)
            {
                foreach (var kvp in root)
                {
                    ctx.Root[kvp.Key] = kvp.Value?.DeepClone();
                }
            }
            if (obj["frames"] is JsonArray frames)
            {
                foreach (var item in frames)
                {
                    if (item is JsonObject fo)
                    {
                        ctx.Frames.Add(DeserializeFrame(fo));
                    }
                }
            }
            return ctx;
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject SerializeFrame(WorkflowFrame f)
    {
        var o = new JsonObject
        {
            ["type"] = f.Type,
            ["nodeId"] = f.NodeId,
            ["index"] = f.Index,
            ["count"] = f.Count,
            ["branchIndex"] = f.BranchIndex,
            ["maxIter"] = f.MaxIter
        };
        if (f.ItemVariable != null) o["itemVariable"] = f.ItemVariable;
        if (f.BodyStart != null) o["bodyStart"] = f.BodyStart;
        if (f.Next != null) o["next"] = f.Next;
        if (f.JoinMode != null) o["joinMode"] = f.JoinMode;
        if (f.Items != null) o["items"] = new JsonArray(f.Items.Select(i => i?.DeepClone()).ToArray());
        if (f.Branches != null) o["branches"] = new JsonArray(f.Branches.Select(b => (JsonNode)b!).ToArray());
        if (f.Results != null) o["results"] = new JsonArray(f.Results.Select(r => (JsonNode)r!).ToArray());
        return o;
    }

    private static WorkflowFrame DeserializeFrame(JsonObject o)
    {
        var f = new WorkflowFrame();
        f.Type = o["type"]?.GetValue<string>() ?? "";
        f.NodeId = o["nodeId"]?.GetValue<string>() ?? "";
        f.Index = o["index"]?.GetValue<int>() ?? 0;
        f.Count = o["count"]?.GetValue<int>() ?? 0;
        f.BranchIndex = o["branchIndex"]?.GetValue<int>() ?? 0;
        f.MaxIter = o["maxIter"]?.GetValue<int>() ?? 0;
        f.ItemVariable = o["itemVariable"]?.GetValue<string>();
        f.BodyStart = o["bodyStart"]?.GetValue<string>();
        f.Next = o["next"]?.GetValue<string>();
        f.JoinMode = o["joinMode"]?.GetValue<string>();
        if (o["items"] is JsonArray items)
        {
            f.Items = items.Select(i => i?.DeepClone()).ToArray();
        }
        if (o["branches"] is JsonArray branches)
        {
            f.Branches = branches.Select(b => b?.GetValue<string>()).Where(s => s != null).Select(s => s!).ToList();
        }
        if (o["results"] is JsonArray results)
        {
            f.Results = results.Select(b => b?.GetValue<bool>() ?? false).ToList();
        }
        return f;
    }
}

/// <summary>Control-flow frame on the run stack (for_each / while / parallel).</summary>
public sealed class WorkflowFrame
{
    public string Type { get; set; } = "";
    public string NodeId { get; set; } = "";

    // for_each
    public JsonNode?[]? Items { get; set; }
    public int Index { get; set; }
    public string? ItemVariable { get; set; }

    // while
    public int Count { get; set; }

    // parallel
    public List<string>? Branches { get; set; }
    public int BranchIndex { get; set; }
    public List<bool>? Results { get; set; }
    public string? JoinMode { get; set; }

    // shared
    public string? BodyStart { get; set; }
    public string? Next { get; set; }
    public int MaxIter { get; set; }
}
