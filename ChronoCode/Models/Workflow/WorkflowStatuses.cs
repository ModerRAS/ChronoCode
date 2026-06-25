namespace ChronoCode.Models.Workflow;

public static class WorkflowNodeStatus
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Retrying = "retrying";
    public const string WaitingApproval = "waiting_approval";
    public const string SchemaValidationFailed = "schema_validation_failed";
    public const string Skipped = "skipped";
}

public static class WorkflowFailureReason
{
    public const string LlmApiError = "llm_api_error";
    public const string TransportError = "transport_error";
    public const string Timeout = "timeout";
    public const string SchemaValidationFailed = "schema_validation_failed";
    public const string MaxAttemptsExceeded = "max_attempts_exceeded";
    public const string ApprovalRejected = "approval_rejected";
}

public static class WorkflowTriggerSource
{
    public const string Scheduled = "scheduled";
    public const string Manual = "manual";
    public const string Retry = "retry";
}

public static class SchedulerStatus
{
    public const string Idle = "idle";
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Paused = "paused";
}

public static class WorkflowBackend
{
    public const string Pi = "pi";
    public const string Opencode = "opencode";
}
