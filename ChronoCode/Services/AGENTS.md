# ChronoCode Services

## OVERVIEW
Service layer implementing the in-app workflow scheduler/dispatcher, the pi/opencode
agent runtimes, Git operations, and the workflow execution engine.

## WHERE TO LOOK

| Service | File | Purpose |
|---------|------|---------|
| AppSchedulerService | AppSchedulerService.cs | ISchedulerService impl: task registration, manual trigger, cron next-run calc, queue snapshot |
| SchedulerBackgroundService | SchedulerBackgroundService.cs | BackgroundService dispatcher: scans due tasks, enforces MaxConcurrentRuns, re-drives retrying nodes |
| WorkflowRunService | Workflow/WorkflowRunService.cs | IWorkflowRunService: creates/runs/resumes workflow runs, drives node executors, stuck-lease recovery |
| Node executors | Workflow/NodeExecutor.cs, Workflow/AgentNodeExecutor.cs | Per-node-type executors (prepare/agent/approval/commit/pr) |
| AgentRuntimeResolver | AgentRuntimeResolver.cs | Resolves IAgentRuntime by backend (pi only for workflow agent nodes) |
| PiRuntime | PiRuntime.cs | pi backend: persistent sessions, resume, steer/follow_up |
| OpencodeRuntime | OpencodeRuntime.cs | opencode backend (legacy AI chat only; not permitted for workflow agent nodes) |
| OpencodeClient | OpencodeClient.cs | HTTP client for opencode AI server |
| OpencodeServerManager | OpencodeServerManager.cs | Process lifecycle for opencode AI server |
| GitService | GitService.cs | git CLI wrapper for clone/commit/PR |
| WorkflowMigration | WorkflowMigration.cs | Backfills legacy Prompt/RequirePlanReview rows into the default workflow graph |

## SERVICE PATTERNS

- Scheduling is an in-app dispatcher (AppSchedulerService + SchedulerBackgroundService); Hangfire was removed.
- Workflow runs are node-graph DSL executions (Models/Workflow/); control flow (condition/for_each/while/parallel) is engine-driven, not LLM-driven.
- Agent node output is a validated JSON envelope (status/passed/summary/artifacts/data) checked against the node dataContract; one in-session schema-repair attempt, then SchemaValidationFailed.
- LLM/transport/timeout failures are externally retried by the dispatcher (node -> retrying -> NextRetryAt), resuming the same pi session; no in-agent retry.
- AgentRuntimeResolver resolves the runtime per node/task backend; workflow agent nodes only accept "pi".
- OpencodeClient receives IHttpClientFactory via constructor.
- GitService is async/await throughout and integrates with the real GitHub API for PR creation.

## NOTES

- Scheduler/dispatcher configured in Program.cs (no Hangfire, no /hangfire dashboard).
- opencode AI server runs as a separate process on port 4096.
- WorkflowNodeExecution + TaskExecution are persisted via EF Core; node-level state is NOT stuffed into TaskExecution.Logs.
- All async methods use await properly, no blocking .Result calls.
