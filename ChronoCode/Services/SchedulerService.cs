using ChronoCode.Models;
using ChronoCode.Models.DTOs;

namespace ChronoCode.Services;

/// <summary>
/// Task registration, manual trigger, next-run calculation and capacity/queue queries.
/// No longer wraps Hangfire; the <see cref="SchedulerBackgroundService"/> is the sole dispatcher.
/// </summary>
public interface ISchedulerService
{
    Task SyncTaskAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task UnscheduleTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task TriggerTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task<List<ScheduledTask>> GetScheduledTasksAsync(CancellationToken cancellationToken = default);
    Task<List<DateTime>> GetNextRunTimesAsync(Guid taskId, int count = 5, CancellationToken cancellationToken = default);
    Task<SchedulerQueueSnapshotDto> GetQueueSnapshotAsync(CancellationToken cancellationToken = default);
}
