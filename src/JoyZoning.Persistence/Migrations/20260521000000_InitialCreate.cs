using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JoyZoning.Persistence.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "config",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                ValueJson = table.Column<string>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_config", x => x.Key));

        migrationBuilder.CreateTable(
            name: "joy_events",
            columns: table => new
            {
                Id = table.Column<long>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CorrelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                Source = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_joy_events", x => x.Id));

        migrationBuilder.CreateTable(
            name: "operator_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                WorkspaceRoot = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                HermesProfile = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                HermesSessionId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                ActiveTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_operator_sessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "work_tasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                OperatorSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                HermesKanbanTaskId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                Title = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                AssignedAgent = table.Column<int>(type: "INTEGER", nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                Risk = table.Column<int>(type: "INTEGER", nullable: false),
                Verification = table.Column<int>(type: "INTEGER", nullable: false),
                LinkedRunId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                LinkedApprovalId = table.Column<string>(type: "TEXT", nullable: true),
                LinkedTerminalSessionId = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_work_tasks", x => x.Id);
                table.ForeignKey(
                    name: "FK_work_tasks_operator_sessions_OperatorSessionId",
                    column: x => x.OperatorSessionId,
                    principalTable: "operator_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "approval_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                ExecutionSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                OperatorSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                HermesRunId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                RequestingAgent = table.Column<int>(type: "INTEGER", nullable: false),
                Category = table.Column<int>(type: "INTEGER", nullable: false),
                Risk = table.Column<int>(type: "INTEGER", nullable: false),
                Command = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                ParametersJson = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                GrantedScope = table.Column<int>(type: "INTEGER", nullable: true),
                RequestedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ResolvedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_approval_requests", x => x.Id));

        migrationBuilder.CreateTable(
            name: "execution_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                HermesRunId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                Objective = table.Column<string>(type: "TEXT", nullable: false),
                ActivePlanJson = table.Column<string>(type: "TEXT", nullable: true),
                Phase = table.Column<int>(type: "INTEGER", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_sessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_execution_sessions_work_tasks_WorkTaskId",
                    column: x => x.WorkTaskId,
                    principalTable: "work_tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "file_snapshots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                Path = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                ChangeKind = table.Column<int>(type: "INTEGER", nullable: false),
                DiffRef = table.Column<string>(type: "TEXT", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_file_snapshots", x => x.Id));

        migrationBuilder.CreateTable(
            name: "terminal_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ExecutionSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                Backend = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table => table.PrimaryKey("PK_terminal_sessions", x => x.Id));

        migrationBuilder.CreateTable(
            name: "execution_steps",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                ExecutionSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                Label = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                DetailJson = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_steps", x => x.Id);
                table.ForeignKey(
                    name: "FK_execution_steps_execution_sessions_ExecutionSessionId",
                    column: x => x.ExecutionSessionId,
                    principalTable: "execution_sessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_joy_events_CorrelationId_Id", table: "joy_events", columns: new[] { "CorrelationId", "Id" });
        migrationBuilder.CreateIndex(name: "IX_joy_events_OccurredAt", table: "joy_events", column: "OccurredAt");
        migrationBuilder.CreateIndex(name: "IX_operator_sessions_Status", table: "operator_sessions", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_work_tasks_OperatorSessionId_Status", table: "work_tasks", columns: new[] { "OperatorSessionId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_execution_sessions_WorkTaskId", table: "execution_sessions", column: "WorkTaskId");
        migrationBuilder.CreateIndex(name: "IX_approval_requests_Status", table: "approval_requests", column: "Status");
        migrationBuilder.CreateIndex(name: "IX_execution_steps_ExecutionSessionId_Ordinal", table: "execution_steps", columns: new[] { "ExecutionSessionId", "Ordinal" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "execution_steps");
        migrationBuilder.DropTable(name: "terminal_sessions");
        migrationBuilder.DropTable(name: "file_snapshots");
        migrationBuilder.DropTable(name: "approval_requests");
        migrationBuilder.DropTable(name: "execution_sessions");
        migrationBuilder.DropTable(name: "work_tasks");
        migrationBuilder.DropTable(name: "joy_events");
        migrationBuilder.DropTable(name: "config");
        migrationBuilder.DropTable(name: "operator_sessions");
    }
}
