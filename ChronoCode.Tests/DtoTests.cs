using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// DTO default value and property tests for CreateTaskDto, UpdateTaskDto,
/// TaskDto, ExecutionDto, NodeExecutionDto, and other DTOs.
/// </summary>
public class DtoTests
{
    // ---- CreateTaskDto ----

    [Fact]
    public void CreateTaskDto_Defaults_AreCorrect()
    {
        var dto = new CreateTaskDto();
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal(string.Empty, dto.CronExpression);
        Assert.Equal("main", dto.BaseBranch);
        Assert.Equal(BranchStrategy.New, dto.BranchStrategy);
        Assert.Equal(600, dto.MaxRuntimeSeconds);
        Assert.Equal(50, dto.MaxFileChanges);
        Assert.True(dto.IsEnabled);
        Assert.Equal("{}", dto.WorkflowDefinitionJson);
        Assert.Equal(1, dto.MaxConcurrentRuns);
        Assert.Equal("{}", dto.NodeFailurePolicyJson);
    }

    [Fact]
    public void CreateTaskDto_CanSetAllProperties()
    {
        var dto = new CreateTaskDto
        {
            Name = "Test",
            CronExpression = "0 0 * * *",
            RepositoryUrl = "https://github.com/x/y",
            BaseBranch = "develop",
            BranchStrategy = BranchStrategy.Reuse,
            MaxRuntimeSeconds = 3600,
            MaxFileChanges = 100,
            IsEnabled = false,
            WorkflowDefinitionJson = "{}",
            DefaultInputsJson = "{\"k\":\"v\"}",
            RuntimeBackend = "pi",
            MaxConcurrentRuns = 5,
            NodeFailurePolicyJson = "{\"maxRetries\":3}"
        };

        Assert.Equal("Test", dto.Name);
        Assert.Equal("develop", dto.BaseBranch);
        Assert.Equal(BranchStrategy.Reuse, dto.BranchStrategy);
        Assert.Equal(3600, dto.MaxRuntimeSeconds);
        Assert.False(dto.IsEnabled);
        Assert.Equal("pi", dto.RuntimeBackend);
        Assert.Equal(5, dto.MaxConcurrentRuns);
    }

    // ---- UpdateTaskDto ----

    [Fact]
    public void UpdateTaskDto_AllPropertiesNullByDefault()
    {
        var dto = new UpdateTaskDto();
        Assert.Null(dto.Name);
        Assert.Null(dto.CronExpression);
        Assert.Null(dto.RepositoryUrl);
        Assert.Null(dto.BaseBranch);
        Assert.Null(dto.BranchStrategy);
        Assert.Null(dto.MaxRuntimeSeconds);
        Assert.Null(dto.MaxFileChanges);
        Assert.Null(dto.IsEnabled);
        Assert.Null(dto.WorkflowDefinitionJson);
        Assert.Null(dto.RuntimeBackend);
        Assert.Null(dto.MaxConcurrentRuns);
    }

    // ---- TaskDto ----

    [Fact]
    public void TaskDto_Defaults_AreCorrect()
    {
        var dto = new TaskDto();
        Assert.Equal(string.Empty, dto.Name);
        Assert.Equal("{}", dto.WorkflowDefinitionJson);
        Assert.Equal("{}", dto.NodeFailurePolicyJson);
        Assert.False(dto.IsEnabled);
        Assert.Equal(0, dto.MaxConcurrentRuns);
        Assert.Equal(SchedulerStatus.Idle, dto.SchedulerStatus);
    }

    // ---- ExecutionDto ----

    [Fact]
    public void ExecutionDto_Defaults_AreCorrect()
    {
        var dto = new ExecutionDto();
        Assert.Equal(WorkflowTriggerSource.Scheduled, dto.TriggerSource);
        Assert.Equal(0, dto.FilesChanged);
    }

    [Fact]
    public void ExecutionDto_CanSetAllProperties()
    {
        var dto = new ExecutionDto
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Status = TaskStatus.Completed,
            WorkflowVersion = 1,
            CurrentNodeId = "commit",
            TriggerSource = WorkflowTriggerSource.Manual,
            BranchName = "chronocode/test",
            CommitSha = "abc123",
            PrUrl = "https://github.com/x/y/pull/1",
            FilesChanged = 5
        };

        Assert.Equal(TaskStatus.Completed, dto.Status);
        Assert.Equal(WorkflowTriggerSource.Manual, dto.TriggerSource);
        Assert.Equal("abc123", dto.CommitSha);
        Assert.Equal(5, dto.FilesChanged);
    }

    // ---- NodeExecutionDto ----

    [Fact]
    public void NodeExecutionDto_Defaults_AreCorrect()
    {
        var dto = new NodeExecutionDto();
        Assert.Equal(string.Empty, dto.NodeId);
        Assert.Equal(string.Empty, dto.NodeType);
        Assert.Equal(string.Empty, dto.Status);
        Assert.Equal(0, dto.Attempt);
        Assert.Equal(0, dto.RetryCount);
    }

    // ---- ExecutionSessionDto ----

    [Fact]
    public void ExecutionSessionDto_Defaults_AreCorrect()
    {
        var dto = new ExecutionSessionDto();
        Assert.False(dto.IsLive);
        Assert.False(dto.SupportsPersistentSessions);
        Assert.False(dto.SupportsSupplementalMessages);
        Assert.False(dto.CanResume);
    }

    // ---- ExecutionMessageDto ----

    [Fact]
    public void ExecutionMessageDto_Defaults_AreCorrect()
    {
        var dto = new ExecutionMessageDto();
        Assert.Equal(string.Empty, dto.Message);
        Assert.Equal("steer", dto.Mode);
    }

    // ---- ApprovalRequestDto ----

    [Fact]
    public void ApprovalRequestDto_DefaultApproved_IsTrue()
    {
        var dto = new ApprovalRequestDto();
        Assert.True(dto.Approved);
        Assert.Null(dto.Reason);
    }

    [Fact]
    public void ApprovalRequestDto_CanSetRejectWithReason()
    {
        var dto = new ApprovalRequestDto { Approved = false, Reason = "Too many changes" };
        Assert.False(dto.Approved);
        Assert.Equal("Too many changes", dto.Reason);
    }

    // ---- LogDto ----

    [Fact]
    public void LogDto_Defaults_AreCorrect()
    {
        var dto = new LogDto();
        Assert.Equal(string.Empty, dto.Level);
        Assert.Equal(string.Empty, dto.Message);
        Assert.Null(dto.Details);
    }

    [Fact]
    public void LogDto_CanSetAllProperties()
    {
        var dto = new LogDto
        {
            Timestamp = DateTime.UtcNow,
            Level = "Error",
            Message = "Something failed",
            Details = "Stack trace..."
        };

        Assert.Equal("Error", dto.Level);
        Assert.Equal("Something failed", dto.Message);
        Assert.Equal("Stack trace...", dto.Details);
    }

    // ---- SchedulerQueueSnapshotDto ----

    [Fact]
    public void SchedulerQueueSnapshot_Defaults_AreCorrect()
    {
        var dto = new SchedulerQueueSnapshotDto();
        Assert.Equal(0, dto.NewRunItems);
        Assert.Equal(0, dto.NodeRetryItems);
        Assert.Equal(0, dto.ActiveRuns);
        Assert.Empty(dto.Items);
    }

    // ---- QueueItemDto ----

    [Fact]
    public void QueueItemDto_Defaults_AreCorrect()
    {
        var dto = new QueueItemDto();
        Assert.Equal(string.Empty, dto.Kind);
        Assert.Null(dto.ExecutionId);
        Assert.Null(dto.TaskId);
        Assert.Null(dto.TaskName);
    }

    // ---- ResumeExecutionSessionDto ----

    [Fact]
    public void ResumeExecutionSessionDto_Defaults_NullSessionRef()
    {
        var dto = new ResumeExecutionSessionDto();
        Assert.Null(dto.SessionRef);
    }

    [Fact]
    public void ResumeExecutionSessionDto_CanSetSessionRef()
    {
        var dto = new ResumeExecutionSessionDto { SessionRef = "session-file.json" };
        Assert.Equal("session-file.json", dto.SessionRef);
    }

    // ---- SetupStatusDto ----

    [Fact]
    public void SetupStatusDto_Defaults_AreCorrect()
    {
        var dto = new SetupStatusDto();
        Assert.False(dto.Initialized);
        Assert.Null(dto.DatabaseProvider);
        Assert.Equal(string.Empty, dto.ConfigFilePath);
        Assert.Equal(string.Empty, dto.DefaultSqlitePath);
    }

    // ---- InitializeSetupDto ----

    [Fact]
    public void InitializeSetupDto_Defaults_AreCorrect()
    {
        var dto = new InitializeSetupDto();
        Assert.Equal("sqlite", dto.DatabaseProvider);
        Assert.Equal(5432, dto.PostgresPort);
    }

    [Fact]
    public void InitializeSetupDto_CanSetPostgresFields()
    {
        var dto = new InitializeSetupDto
        {
            DatabaseProvider = "postgresql",
            PostgresHost = "db.example.com",
            PostgresPort = 6543,
            PostgresDatabase = "chronocode",
            PostgresUsername = "admin",
            PostgresPassword = "secret"
        };

        Assert.Equal("postgresql", dto.DatabaseProvider);
        Assert.Equal("db.example.com", dto.PostgresHost);
        Assert.Equal(6543, dto.PostgresPort);
        Assert.Equal("chronocode", dto.PostgresDatabase);
        Assert.Equal("admin", dto.PostgresUsername);
        Assert.Equal("secret", dto.PostgresPassword);
    }

    [Fact]
    public void InitializeSetupDto_CanSetSqliteFields()
    {
        var dto = new InitializeSetupDto
        {
            DatabaseProvider = "sqlite",
            SqlitePath = "/data/chronocode.db"
        };

        Assert.Equal("sqlite", dto.DatabaseProvider);
        Assert.Equal("/data/chronocode.db", dto.SqlitePath);
    }

    [Fact]
    public void InitializeSetupDto_CanSetConnectionString()
    {
        var dto = new InitializeSetupDto
        {
            DatabaseProvider = "postgresql",
            ConnectionString = "Host=db;Database=mydb"
        };

        Assert.Equal("Host=db;Database=mydb", dto.ConnectionString);
    }
}
