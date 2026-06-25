using System.Text.Json;
using System.Text.Json.Nodes;

namespace ChronoCode.Models.Workflow;

public static class WorkflowDefinitionSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static readonly JsonSerializerOptions PrettyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(WorkflowDefinition definition) =>
        JsonSerializer.Serialize(definition, Options);

    public static string SerializePretty(WorkflowDefinition definition) =>
        JsonSerializer.Serialize(definition, PrettyOptions);

    public static WorkflowDefinition? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkflowDefinition>(json, Options);
    }

    public static WorkflowNodeFailurePolicy? DeserializeFailurePolicy(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<WorkflowNodeFailurePolicy>(json, Options);
    }

    public static string SerializeFailurePolicy(WorkflowNodeFailurePolicy? policy) =>
        policy == null ? "{}" : JsonSerializer.Serialize(policy, Options);

    public static JsonNode? ParseJsonNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }
}
