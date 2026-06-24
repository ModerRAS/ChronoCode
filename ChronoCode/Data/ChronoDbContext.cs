using Microsoft.EntityFrameworkCore;
using ChronoCode.Models;
using ChronoCode.Models.Workflow;

namespace ChronoCode.Data;

public class ChronoDbContext : DbContext
{
    public ChronoDbContext(DbContextOptions<ChronoDbContext> options) : base(options)
    {
    }

    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<TaskExecution> TaskExecutions => Set<TaskExecution>();
    public DbSet<WorkflowNodeExecution> WorkflowNodeExecutions => Set<WorkflowNodeExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ScheduledTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.CronExpression).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RepositoryUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.BaseBranch).HasMaxLength(100).HasDefaultValue("main");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LastRunAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.NextRunAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.LastQueuedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.SchedulerHeartbeatAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.BranchStrategy)
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(e => e.LastStatus)
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(e => e.WorkflowDefinitionJson).IsRequired();
            entity.Property(e => e.RuntimeBackend).HasMaxLength(32);
            entity.Property(e => e.MaxConcurrentRuns).HasDefaultValue(1);
            entity.Property(e => e.NodeFailurePolicyJson).HasDefaultValue("{}");
            entity.Property(e => e.SchedulerStatus).HasMaxLength(32).HasDefaultValue("idle");

            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.LastStatus);
            entity.HasIndex(e => e.IsEnabled);
            entity.HasIndex(e => e.NextRunAt);
            entity.HasIndex(e => e.SchedulerStatus);
        });

        modelBuilder.Entity<TaskExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.StartedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.Status)
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(e => e.WorkflowSnapshotJson).HasColumnType("text");
            entity.Property(e => e.CurrentNodeId).HasMaxLength(128);
            entity.Property(e => e.TriggerSource).HasMaxLength(32).HasDefaultValue("scheduled");
            entity.Property(e => e.BranchName).HasMaxLength(200);
            entity.Property(e => e.CommitSha).HasMaxLength(40);
            entity.Property(e => e.PrUrl).HasMaxLength(500);
            entity.Property(e => e.WorkflowStateJson).HasColumnType("text");

            entity.HasIndex(e => e.TaskId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartedAt);

            entity.HasOne<ScheduledTask>()
                  .WithMany()
                  .HasForeignKey(e => e.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Logs)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
                  .HasColumnType("jsonb");
        });

        modelBuilder.Entity<WorkflowNodeExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.Property(e => e.ExecutionId).IsRequired();
            entity.Property(e => e.NodeId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.NodeType).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ScopeKey).HasMaxLength(256);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.Property(e => e.StartedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CompletedAt).HasColumnType("timestamp with time zone");

            entity.Property(e => e.InputJson).HasColumnType("text");
            entity.Property(e => e.OutputJson).HasColumnType("text");
            entity.Property(e => e.ValidationError).HasColumnType("text");

            entity.Property(e => e.AgentBackend).HasMaxLength(32);
            entity.Property(e => e.AgentSessionId).HasMaxLength(128);
            entity.Property(e => e.AgentSessionFile).HasMaxLength(1024);
            entity.Property(e => e.AgentWorkingDirectory).HasMaxLength(1024);
            entity.Property(e => e.FailureReason).HasMaxLength(64);

            entity.Property(e => e.NextRetryAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.LeaseExpiresAt).HasColumnType("timestamp with time zone");
            entity.Property(e => e.SchemaRepairAttempted).HasDefaultValue(false);

            entity.HasIndex(e => e.ExecutionId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.NextRetryAt);
            entity.HasIndex(e => e.LeaseExpiresAt);
            entity.HasIndex(e => new { e.ExecutionId, e.NodeId, e.ScopeKey });

            entity.HasOne<TaskExecution>()
                  .WithMany()
                  .HasForeignKey(e => e.ExecutionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
