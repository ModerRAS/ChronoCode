using System.Text.Json;
using System.Text.Json.Nodes;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>
/// Validates raw agent output against the required JSON envelope
/// ({status,passed,summary,artifacts,data}) plus the node's data contract.
/// </summary>
public static class AgentOutputValidator
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "completed", "blocked", "failed"
    };

    public static bool ValidateAgentOutput(
        string? rawResponse,
        WorkflowDataContract contract,
        out JsonNode validatedEnvelope,
        out string error)
    {
        validatedEnvelope = null!;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            error = "Agent returned an empty response.";
            return false;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(rawResponse);
        }
        catch (JsonException ex)
        {
            error = $"Agent response is not valid JSON: {ex.Message}";
            return false;
        }

        if (parsed is not JsonObject obj)
        {
            error = "Agent response must be a JSON object envelope.";
            return false;
        }

        if (!obj.TryGetPropertyValue("status", out var statusNode) ||
            statusNode is not JsonValue statusVal ||
            !statusVal.TryGetValue<string>(out var status) ||
            !ValidStatuses.Contains(status))
        {
            error = "Envelope missing or invalid 'status' (expected completed|blocked|failed).";
            return false;
        }

        if (obj.TryGetPropertyValue("passed", out var passedNode) && passedNode != null)
        {
            if (passedNode is not JsonValue pv || !pv.TryGetValue<bool>(out _))
            {
                error = "Envelope field 'passed' must be a boolean or null.";
                return false;
            }
        }

        if (!obj.TryGetPropertyValue("summary", out var summaryNode) ||
            summaryNode is not JsonValue summaryVal ||
            !summaryVal.TryGetValue<string>(out _))
        {
            error = "Envelope missing or invalid 'summary' (expected string).";
            return false;
        }

        if (!obj.TryGetPropertyValue("artifacts", out var artifactsNode) ||
            artifactsNode is not JsonArray artifacts)
        {
            error = "Envelope missing or invalid 'artifacts' (expected array).";
            return false;
        }

        foreach (var artifact in artifacts)
        {
            if (artifact is not JsonValue av || !av.TryGetValue<string>(out _))
            {
                error = "Every entry in 'artifacts' must be a string.";
                return false;
            }
        }

        if (!obj.TryGetPropertyValue("data", out var dataNode) || dataNode is not JsonObject data)
        {
            error = "Envelope missing or invalid 'data' (expected object).";
            return false;
        }

        if (contract?.Fields != null)
        {
            foreach (var field in contract.Fields)
            {
                var present = data.TryGetPropertyValue(field.Name, out var fieldValue);
                if (!present || fieldValue == null)
                {
                    if (field.Required)
                    {
                        error = $"Missing required data field '{field.Name}'.";
                        return false;
                    }
                    continue;
                }

                if (!TypeMatches(fieldValue, field.Type))
                {
                    error = $"Data field '{field.Name}' does not match expected type {field.Type}.";
                    return false;
                }
            }
        }

        validatedEnvelope = parsed;
        return true;
    }

    private static bool TypeMatches(JsonNode node, WorkflowDataType expected)
    {
        return expected switch
        {
            WorkflowDataType.String => node is JsonValue v1 && v1.TryGetValue<string>(out _),
            WorkflowDataType.Number => node is JsonValue v2 && (v2.TryGetValue<decimal>(out _) || v2.TryGetValue<double>(out _) || v2.TryGetValue<long>(out _)),
            WorkflowDataType.Boolean => node is JsonValue v3 && v3.TryGetValue<bool>(out _),
            WorkflowDataType.Object => node is JsonObject,
            WorkflowDataType.Array => node is JsonArray,
            _ => false
        };
    }
}
