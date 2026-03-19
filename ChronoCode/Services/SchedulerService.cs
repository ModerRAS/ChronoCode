using ChronoCode.Models;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;

namespace ChronoCode.Services;

public interface ISchedulerService
{
    void ScheduleTask(ScheduledTask task);
    void UnscheduleTask(Guid taskId);
    void TriggerTask(Guid taskId);
    List<ScheduledTask> GetScheduledTasks();
    List<DateTime> GetNextRunTimes(Guid taskId, int count = 5);
}

public class HangfireSchedulerService : ISchedulerService
{
    private readonly ITaskRunner _taskRunner;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<HangfireSchedulerService> _logger;

    public HangfireSchedulerService(
        ITaskRunner taskRunner,
        ITaskRepository taskRepository,
        ILogger<HangfireSchedulerService> logger)
    {
        _taskRunner = taskRunner;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public void ScheduleTask(ScheduledTask task)
    {
        var jobId = $"task_{task.Id}";

        RecurringJob.AddOrUpdate<ScheduledTaskJob>(
            jobId,
            job => job.ExecuteAsync(task.Id),
            task.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Local,
                MisfireHandling = MisfireHandlingMode.Relaxed
            });

        _logger.LogInformation("Scheduled task {TaskId} with cron: {Cron}", task.Id, task.CronExpression);
    }

    public void UnscheduleTask(Guid taskId)
    {
        var jobId = $"task_{taskId}";
        RecurringJob.RemoveIfExists(jobId);
        _logger.LogInformation("Unscheduled task {TaskId}", taskId);
    }

    public void TriggerTask(Guid taskId)
    {
        var jobId = $"manual_{taskId}_{DateTime.UtcNow:yyyyMMddHHmmss}";
        
        BackgroundJob.Enqueue<ScheduledTaskJob>(
            jobId,
            job => job.ExecuteAsync(taskId));

        _logger.LogInformation("Triggered task {TaskId} with job id: {JobId}", taskId, jobId);
    }

    public List<ScheduledTask> GetScheduledTasks()
    {
        using var connection = JobStorage.Current.GetConnection();
        var jobs = connection.GetRecurringJobs();
        
        var taskIds = jobs
            .Where(j => j.Id.StartsWith("task_"))
            .Select(j => j.Id.Replace("task_", ""))
            .Select(Guid.Parse)
            .ToList();

        return taskIds
            .Select(id => _taskRepository.GetByIdAsync(id).GetAwaiter().GetResult())
            .Where(t => t != null)
            .Cast<ScheduledTask>()
            .ToList();
    }

    public List<DateTime> GetNextRunTimes(Guid taskId, int count = 5)
    {
        var jobId = $"task_{taskId}";
        using var connection = JobStorage.Current.GetConnection();
        var job = connection.GetRecurringJobs().FirstOrDefault(j => j.Id == jobId);

        if (job?.NextExecution == null)
            return new List<DateTime>();

        var result = new List<DateTime>();
        var next = job.NextExecution.Value;

        for (int i = 0; i < count; i++)
        {
            result.Add(next);
            next = next.AddMinutes(1);
        }

        return result;
    }
}

public class ScheduledTaskJob
{
    private readonly ITaskRunner _taskRunner;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<ScheduledTaskJob> _logger;

    public ScheduledTaskJob(
        ITaskRunner taskRunner,
        ITaskRepository taskRepository,
        ILogger<ScheduledTaskJob> logger)
    {
        _taskRunner = taskRunner;
        _taskRepository = taskRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid taskId)
    {
        _logger.LogInformation("Executing scheduled task: {TaskId}", taskId);

        var task = await _taskRepository.GetByIdAsync(taskId);
        if (task == null)
        {
            _logger.LogError("Task not found: {TaskId}", taskId);
            return;
        }

        if (!task.IsEnabled)
        {
            _logger.LogInformation("Task disabled, skipping: {TaskId}", taskId);
            return;
        }

        try
        {
            await _taskRunner.ExecuteTaskAsync(task);
            await _taskRepository.UpdateLastRunAsync(taskId, Models.TaskStatus.Completed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task execution failed: {TaskId}", taskId);
            await _taskRepository.UpdateLastRunAsync(taskId, Models.TaskStatus.Failed, ex.Message);
        }
    }
}
