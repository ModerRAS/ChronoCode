using System.Net.Http;
using System.Net.Sockets;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Services.Workflow;

/// <summary>
/// Maps a runtime exception to an external-retry reason. Returns null when the
/// exception is not retryable.
/// </summary>
public static class FailureClassifier
{
    public static WorkflowRetryReason? Classify(Exception ex)
    {
        if (ex is OperationCanceledException || ex is TimeoutException)
        {
            return WorkflowRetryReason.Timeout;
        }

        if (ex is IOException || ex is HttpRequestException || ex is SocketException)
        {
            return WorkflowRetryReason.TransportError;
        }

        if (ex is InvalidOperationException)
        {
            return WorkflowRetryReason.LlmApiError;
        }

        return null;
    }
}
