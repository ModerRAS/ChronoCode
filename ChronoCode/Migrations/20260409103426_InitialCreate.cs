using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChronoCode.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RepositoryUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    BaseBranch = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "main"),
                    BranchStrategy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    MaxRuntimeSeconds = table.Column<int>(type: "integer", nullable: false),
                    MaxFileChanges = table.Column<int>(type: "integer", nullable: false),
                    RequirePlanReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskExecutions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CommitSha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PrUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    FilesChanged = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Logs = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Info"),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskLogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_IsEnabled",
                table: "ScheduledTasks",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_LastStatus",
                table: "ScheduledTasks",
                column: "LastStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledTasks_Name",
                table: "ScheduledTasks",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_StartedAt",
                table: "TaskExecutions",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_Status",
                table: "TaskExecutions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutions_TaskId",
                table: "TaskExecutions",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledTasks");

            migrationBuilder.DropTable(
                name: "TaskExecutions");

            migrationBuilder.DropTable(
                name: "TaskLogEntries");
        }
    }
}
