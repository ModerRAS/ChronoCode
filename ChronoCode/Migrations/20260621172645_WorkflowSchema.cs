using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChronoCode.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequirePlanReview",
                table: "ScheduledTasks");

            migrationBuilder.RenameColumn(
                name: "Prompt",
                table: "ScheduledTasks",
                newName: "WorkflowDefinitionJson");

            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeId",
                table: "TaskExecutions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerSource",
                table: "TaskExecutions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "scheduled");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowSnapshotJson",
                table: "TaskExecutions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "WorkflowStateJson",
                table: "TaskExecutions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersion",
                table: "TaskExecutions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DefaultInputsJson",
                table: "ScheduledTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastQueuedAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxConcurrentRuns",
                table: "ScheduledTasks",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRunAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeFailurePolicyJson",
                table: "ScheduledTasks",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "RuntimeBackend",
                table: "ScheduledTasks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SchedulerHeartbeatAt",
                table: "ScheduledTasks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchedulerStatus",
                table: "ScheduledTasks",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "idle");

            migrationBuilder.AddColumn<int>(
                name: "WorkflowVersion",
                table: "ScheduledTasks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkflowNodeExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    NodeType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InputJson = table.Column<string>(type: "text", nullable: true),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    ValidationError = table.Column<string>(type: "text", nullable: true),
                    AgentBackend = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AgentSessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AgentSessionFile = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    AgentWorkingDirectory = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    LeaseExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SchemaRepairAttempted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowNodeExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowNodeExecutions_TaskExecutions_ExecutionId",
                        column: x => x.ExecutionId,
                        principalTable: "TaskExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_NextRunAt",
                table: "ScheduledTasks",
                column: "NextRunAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_SchedulerStatus",
                table: "ScheduledTasks",
                column: "SchedulerStatus");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNodeExecutions_ExecutionId",
                table: "WorkflowNodeExecutions",
                column: "ExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNodeExecutions_ExecutionId_NodeId_ScopeKey",
                table: "WorkflowNodeExecutions",
                columns: new[] { "ExecutionId", "NodeId", "ScopeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNodeExecutions_LeaseExpiresAt",
                table: "WorkflowNodeExecutions",
                column: "LeaseExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNodeExecutions_NextRetryAt",
                table: "WorkflowNodeExecutions",
                column: "NextRetryAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowNodeExecutions_Status",
                table: "WorkflowNodeExecutions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkflowNodeExecutions");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTasks_NextRunAt",
                table: "ScheduledTasks");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledTasks_SchedulerStatus",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "TriggerSource",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "WorkflowSnapshotJson",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "WorkflowStateJson",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "WorkflowVersion",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "DefaultInputsJson",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "LastQueuedAt",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "MaxConcurrentRuns",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "NextRunAt",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "NodeFailurePolicyJson",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "RuntimeBackend",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "SchedulerHeartbeatAt",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "SchedulerStatus",
                table: "ScheduledTasks");

            migrationBuilder.DropColumn(
                name: "WorkflowVersion",
                table: "ScheduledTasks");

            migrationBuilder.RenameColumn(
                name: "WorkflowDefinitionJson",
                table: "ScheduledTasks",
                newName: "Prompt");

            migrationBuilder.AddColumn<bool>(
                name: "RequirePlanReview",
                table: "ScheduledTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
