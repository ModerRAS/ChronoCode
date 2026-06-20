using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChronoCode.Migrations
{
    /// <inheritdoc />
    public partial class PendingModelSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_TaskExecutions_ScheduledTasks_TaskId",
                table: "TaskExecutions",
                column: "TaskId",
                principalTable: "ScheduledTasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskExecutions_ScheduledTasks_TaskId",
                table: "TaskExecutions");
        }
    }
}
