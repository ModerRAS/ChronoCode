using ChronoCode.Models;
using ChronoCode.Models.DTOs;
using ChronoCode.Models.Workflow;
using ChronoCode.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using TaskStatus = ChronoCode.Models.TaskStatus;

namespace ChronoCode.Tests;

/// <summary>
/// Edge-case tests for InMemoryExecutionRepository and InMemoryTaskRepository
/// that go beyond what WorkflowRunServiceTests exercises indirectly.
/// </summary>
public class RepositoryEdgeCaseTests
{
    // ---- InMemoryExecutionRepository ----

    private static InMemoryExecutionRepository CreateExecRepo() =>
        new(NullLogger<InMemoryExecutionRepository>.Instance);

    [Fact]
    public async Task Exec_CreateAndGetById_ReturnsExecution()
    {
        var repo = CreateExecRepo();
        var exec = new TaskExecution
        {
            Id = Guid.NewGuid(),
            TaskId = Guid.NewGuid(),
            Status = TaskStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        await repo.CreateAsync(exec);
        var found = await repo.GetByIdAsync(exec.Id);

        Assert.NotNull(found);
        Assert.Equal(exec.Id, found!.Id);
    }

    [Fact]
    public async Task Exec_GetById_NotFound_ReturnsNull()
    {
        var repo = CreateExecRepo();
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Exec_GetByTaskId_RespectsLimit()
    {
        var repo = CreateExecRepo();
        var taskId = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            await repo.CreateAsync(new TaskExecution
            {
                Id = Guid.NewGuid(),
                TaskId = taskId,
                Status = TaskStatus.Completed,
                StartedAt = DateTime.UtcNow.AddSeconds(-i)
            });
        }

        var result = await repo.GetByTaskIdAsync(taskId, limit: 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task Exec_GetActiveRuns_ReturnsOnlyRunning()
    {
        var repo = CreateExecRepo();
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Completed, StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = Guid.NewGuid(), Status = TaskStatus.Failed, StartedAt = DateTime.UtcNow });

        var active = await repo.GetActiveRunsAsync();
        Assert.Single(active);
        Assert.Equal(TaskStatus.Running, active[0].Status);
    }

    [Fact]
    public async Task Exec_CountActiveRuns_ReturnsCorrectCount()
    {
        var repo = CreateExecRepo();
        var taskId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });
        await repo.CreateAsync(new TaskExecution { Id = Guid.NewGuid(), TaskId = taskId, Status = TaskStatus.Completed, StartedAt = DateTime.UtcNow });

        var count = await repo.CountActiveRunsAsync(taskId);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Exec_CreateAndGetNodeExecution_ReturnsNode()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execId,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await repo.CreateNodeExecutionAsync(node);

        var found = await repo.GetNodeExecutionAsync(node.Id);
        Assert.NotNull(found);
        Assert.Equal("agent", found!.NodeId);
    }

    [Fact]
    public async Task Exec_GetNodeExecutions_ReturnsAllForExecution()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "start", NodeType = "start", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Completed, StartedAt = DateTime.UtcNow });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Completed, StartedAt = DateTime.UtcNow });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "end", NodeType = "end", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Completed, StartedAt = DateTime.UtcNow });

        var nodes = await repo.GetNodeExecutionsAsync(execId);
        Assert.Equal(3, nodes.Count);
    }

    [Fact]
    public async Task Exec_GetRunningNodeExecutions_ReturnsOnlyRunning()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent1", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Running, StartedAt = DateTime.UtcNow, LeaseExpiresAt = DateTime.UtcNow.AddSeconds(90) });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent2", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Completed, StartedAt = DateTime.UtcNow });

        var running = await repo.GetRunningNodeExecutionsAsync();
        Assert.Single(running);
        Assert.Equal("agent1", running[0].NodeId);
    }

    [Fact]
    public async Task Exec_GetRetryableNodeExecutions_ReturnsDueRetries()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        var pastRetry = DateTime.UtcNow.AddSeconds(-10);
        var futureRetry = DateTime.UtcNow.AddSeconds(60);

        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent1", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Retrying, StartedAt = DateTime.UtcNow, NextRetryAt = pastRetry });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent2", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Retrying, StartedAt = DateTime.UtcNow, NextRetryAt = futureRetry });
        await repo.CreateNodeExecutionAsync(new WorkflowNodeExecution { Id = Guid.NewGuid(), ExecutionId = execId, NodeId = "agent3", NodeType = "agent", ScopeKey = "root", Attempt = 0, Status = WorkflowNodeStatus.Completed, StartedAt = DateTime.UtcNow });

        var retryable = await repo.GetRetryableNodeExecutionsAsync(DateTime.UtcNow);
        Assert.Single(retryable);
        Assert.Equal("agent1", retryable[0].NodeId);
    }

    [Fact]
    public async Task Exec_GetActiveNodeExecution_ReturnsLatestIncludingCompleted()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execId,
            NodeId = "gate",
            NodeType = "approval_gate",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.Completed,
            StartedAt = DateTime.UtcNow
        };
        await repo.CreateNodeExecutionAsync(node);

        var active = await repo.GetActiveNodeExecutionAsync(execId, "gate", "root");
        Assert.NotNull(active);
        Assert.Equal(WorkflowNodeStatus.Completed, active!.Status);
    }

    [Fact]
    public async Task Exec_GetWaitingApprovalNode_ReturnsCorrectNode()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execId,
            NodeId = "gate",
            NodeType = "approval_gate",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.WaitingApproval,
            StartedAt = DateTime.UtcNow
        };
        await repo.CreateNodeExecutionAsync(node);

        var found = await repo.GetWaitingApprovalNodeAsync(execId, node.Id);
        Assert.NotNull(found);
        Assert.Equal(WorkflowNodeStatus.WaitingApproval, found!.Status);
    }

    [Fact]
    public async Task Exec_AddLogAndGetLogs_ReturnsEntries()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        await repo.AddLogAsync(execId, "Info", "Started");
        await repo.AddLogAsync(execId, "Warning", "Slow");
        await repo.AddLogAsync(execId, "Error", "Failed", "details");

        var logs = await repo.GetLogsAsync(execId);
        Assert.Equal(3, logs.Count);
        Assert.Equal("Started", logs[0].Message);
        Assert.Equal("Error", logs[2].Level);
        Assert.Equal("details", logs[2].Details);
    }

    [Fact]
    public async Task Exec_UpdateNodeExecution_PersistsChanges()
    {
        var repo = CreateExecRepo();
        var execId = Guid.NewGuid();
        await repo.CreateAsync(new TaskExecution { Id = execId, TaskId = Guid.NewGuid(), Status = TaskStatus.Running, StartedAt = DateTime.UtcNow });

        var node = new WorkflowNodeExecution
        {
            Id = Guid.NewGuid(),
            ExecutionId = execId,
            NodeId = "agent",
            NodeType = "agent",
            ScopeKey = "root",
            Attempt = 0,
            Status = WorkflowNodeStatus.Running,
            StartedAt = DateTime.UtcNow
        };
        await repo.CreateNodeExecutionAsync(node);

        node.Status = WorkflowNodeStatus.Completed;
        node.OutputJson = """{"passed":true}""";
        node.CompletedAt = DateTime.UtcNow;
        await repo.UpdateNodeExecutionAsync(node);

        var updated = await repo.GetNodeExecutionAsync(node.Id);
        Assert.Equal(WorkflowNodeStatus.Completed, updated!.Status);
        Assert.Contains("passed", updated.OutputJson!);
    }

    // ---- InMemoryTaskRepository ----

    private static InMemoryTaskRepository CreateTaskRepo() =>
        new(NullLogger<InMemoryTaskRepository>.Instance);

    private static CreateTaskDto ValidCreateDto() => new()
    {
        Name = "Test Task",
        CronExpression = "0 0 * * *",
        RepositoryUrl = "https://github.com/test/repo",
        BaseBranch = "main",
        BranchStrategy = BranchStrategy.New,
        MaxRuntimeSeconds = 600,
        MaxFileChanges = 50,
        IsEnabled = true,
        WorkflowDefinitionJson = Models.Workflow.WorkflowDefinitionFactory.CreateDefaultJson(false, "do work"),
        MaxConcurrentRuns = 1,
        NodeFailurePolicyJson = "{}"
    };

    [Fact]
    public async Task Task_CreateAndGetById_ReturnsTask()
    {
        var repo = CreateTaskRepo();
        var task = await repo.CreateAsync(ValidCreateDto());

        Assert.NotNull(task);
        var found = await repo.GetByIdAsync(task.Id);
        Assert.NotNull(found);
        Assert.Equal("Test Task", found!.Name);
    }

    [Fact]
    public async Task Task_GetAll_ReturnsAllTasks()
    {
        var repo = CreateTaskRepo();
        await repo.CreateAsync(ValidCreateDto());
        var dto2 = ValidCreateDto();
        dto2.Name = "Task 2";
        await repo.CreateAsync(dto2);

        var all = await repo.GetAllAsync();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Task_Update_PartialUpdate_PreservesUnsetFields()
    {
        var repo = CreateTaskRepo();
        var task = await repo.CreateAsync(ValidCreateDto());

        await repo.UpdateAsync(task.Id, new UpdateTaskDto { Name = "Updated Name" });

        var updated = await repo.GetByIdAsync(task.Id);
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("0 0 * * *", updated.CronExpression); // unchanged
        Assert.Equal("https://github.com/test/repo", updated.RepositoryUrl); // unchanged
    }

    [Fact]
    public async Task Task_Delete_RemovesTask()
    {
        var repo = CreateTaskRepo();
        var task = await repo.CreateAsync(ValidCreateDto());

        var deleted = await repo.DeleteAsync(task.Id);
        Assert.True(deleted);
        Assert.Null(await repo.GetByIdAsync(task.Id));
    }

    [Fact]
    public async Task Task_Delete_NotFound_ReturnsFalse()
    {
        var repo = CreateTaskRepo();
        Assert.False(await repo.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Task_UpdateLastRun_SetsStatusAndError()
    {
        var repo = CreateTaskRepo();
        var task = await repo.CreateAsync(ValidCreateDto());

        await repo.UpdateLastRunAsync(task.Id, TaskStatus.Failed, "something went wrong");

        var updated = await repo.GetByIdAsync(task.Id);
        Assert.Equal(TaskStatus.Failed, updated!.LastStatus);
        Assert.Equal("something went wrong", updated.LastError);
    }

    [Fact]
    public async Task Task_GetDueTasks_ReturnsOnlyDueEnabledTasks()
    {
        var repo = CreateTaskRepo();
        var task = await repo.CreateAsync(ValidCreateDto());

        var past = DateTime.UtcNow.AddSeconds(-30);
        await repo.UpdateSchedulerStateAsync(task.Id, past, DateTime.UtcNow, SchedulerStatus.Queued, DateTime.UtcNow);

        var due = await repo.GetDueTasksAsync(DateTime.UtcNow);
        Assert.Single(due);
        Assert.Equal(task.Id, due[0].Id);
    }

    [Fact]
    public async Task Task_GetDueTasks_ExcludesDisabledTasks()
    {
        var repo = CreateTaskRepo();
        var dto = ValidCreateDto();
        dto.IsEnabled = false;
        var task = await repo.CreateAsync(dto);

        var past = DateTime.UtcNow.AddSeconds(-30);
        await repo.UpdateSchedulerStateAsync(task.Id, past, DateTime.UtcNow, SchedulerStatus.Queued, DateTime.UtcNow);

        var due = await repo.GetDueTasksAsync(DateTime.UtcNow);
        Assert.Empty(due);
    }
}
