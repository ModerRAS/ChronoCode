using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChronoCode.Migrations
{
    /// <inheritdoc />
    public partial class PersistExecutionAgentSessionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentBackend",
                table: "TaskExecutions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentSessionFile",
                table: "TaskExecutions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentSessionId",
                table: "TaskExecutions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentWorkingDirectory",
                table: "TaskExecutions",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgentBackend",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "AgentSessionFile",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "AgentSessionId",
                table: "TaskExecutions");

            migrationBuilder.DropColumn(
                name: "AgentWorkingDirectory",
                table: "TaskExecutions");
        }
    }
}
