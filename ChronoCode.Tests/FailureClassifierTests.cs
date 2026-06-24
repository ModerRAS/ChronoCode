using System.Net.Http;
using System.Net.Sockets;
using ChronoCode.Models.Workflow;
using ChronoCode.Services.Workflow;
using Xunit;

namespace ChronoCode.Tests;

public class FailureClassifierTests
{
    [Fact]
    public void Classify_TimeoutException_ReturnsTimeout()
    {
        Assert.Equal(WorkflowRetryReason.Timeout, FailureClassifier.Classify(new TimeoutException()));
    }

    [Fact]
    public void Classify_OperationCanceledException_ReturnsTimeout()
    {
        Assert.Equal(WorkflowRetryReason.Timeout, FailureClassifier.Classify(new OperationCanceledException()));
    }

    [Fact]
    public void Classify_HttpRequestException_ReturnsTransportError()
    {
        Assert.Equal(WorkflowRetryReason.TransportError, FailureClassifier.Classify(new HttpRequestException("503")));
    }

    [Fact]
    public void Classify_IOException_ReturnsTransportError()
    {
        Assert.Equal(WorkflowRetryReason.TransportError, FailureClassifier.Classify(new IOException("stream broken")));
    }

    [Fact]
    public void Classify_SocketException_ReturnsTransportError()
    {
        Assert.Equal(WorkflowRetryReason.TransportError, FailureClassifier.Classify(new SocketException()));
    }

    [Fact]
    public void Classify_InvalidOperationException_ReturnsLlmApiError()
    {
        Assert.Equal(WorkflowRetryReason.LlmApiError, FailureClassifier.Classify(new InvalidOperationException("llm api down")));
    }

    [Fact]
    public void Classify_ArgumentException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new ArgumentException("not retryable")));
    }

    [Fact]
    public void Classify_NullException_ReturnsNull()
    {
        Assert.Null(FailureClassifier.Classify(new Exception("generic")));
    }
}
