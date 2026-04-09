# ChronoCode Services

## OVERVIEW
Service layer implementing Hangfire scheduling, Opencode AI integration, Git operations, and task execution.

## WHERE TO LOOK

| Service | File | Purpose |
|---------|------|---------|
| HangfireSchedulerService | SchedulerService.cs | Cron-triggered job registration |
| OpencodeClient | OpencodeClient.cs | HTTP client for AI server communication |
| OpencodeServerManager | OpencodeServerManager.cs | Process lifecycle management for AI server |
| GitService | GitService.cs | LibGit2Sharp wrapper for clone/commit/PR |
| TaskRunner | TaskRunner.cs | Executes AI-generated tasks |

## SERVICE PATTERNS

- Hangfire jobs registered via AddRecurringJob<TService>(...) extension pattern
- OpencodeClient receives IHttpClientFactory via constructor (correct pattern)
- GitService uses async/await throughout for I/O operations
- GitService integrates with real GitHub API for PR creation
- TaskRunner manages task lifecycle with progress callbacks

## NOTES

- Hangfire configured directly in Program.cs, not in a separate configuration class
- Opencode AI server runs as separate process on port 4096
- In-memory state used for job execution tracking
- All async methods use await properly, no blocking .Result calls
