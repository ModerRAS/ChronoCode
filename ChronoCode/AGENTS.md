# ChronoCode Backend

**OVERVIEW:** .NET 10 minimal API backend with Hangfire job scheduling, EF Core persistence, and AI integration.

## STRUCTURE

```
ChronoCode/
├── Controllers/           # REST endpoints (TasksController, AIController)
├── Services/             # Business logic (Hangfire, Git, AI, TaskRunner)
├── Models/               # Entities + DTOs (ScheduledTask, TaskExecution)
├── Data/                # ChronoDbContext (PostgreSQL via EF Core)
├── Middleware/           # ExceptionHandlingMiddleware
├── Validators/           # FluentValidation rules
└── Program.cs           # DI setup, minimal API host
```

## WHERE TO LOOK

| Task | Location | Notes |
|------|----------|-------|
| Add API endpoint | Controllers/ | REST conventions, DI in Program.cs |
| Background job logic | Services/SchedulerService.cs | Cron-triggered job registration |
| AI integration | Services/OpencodeClient.cs, OpencodeServerManager.cs | HTTP client for AI server |
| Git operations | Services/GitService.cs | Clone, commit, PR creation |
| Data persistence | Data/ChronoDbContext.cs | EF Core with PostgreSQL |
| Run tests | ChronoCode.Tests/ | xUnit with WebApplicationFactory |

## CONVENTIONS

- Minimal API top-level pattern (no Startup.cs)
- Hangfire embedded directly in Program.cs
- IHttpClientFactory for all HTTP clients
- FluentValidation for DTO validation
- Nullable enable, ImplicitUsings enable
- No .editorconfig/stylecop (relies on SDK defaults)

## ANTI-PATTERNS

- **DO NOT** use `.Result` on async calls. Causes deadlocks in ASP.NET Core.
- **DO NOT** create `new HttpClient()` directly. Use IHttpClientFactory.
- **DO NOT** skip Hangfire auth check. Dashboard is unprotected.
- **DO NOT** commit appsettings.json with real credentials.

## COMMANDS

```bash
dotnet build ChronoCode.sln
dotnet run --project ChronoCode
dotnet test ChronoCode.Tests
```
