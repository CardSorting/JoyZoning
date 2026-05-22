using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JoyZoning.Persistence.Migrations;

public partial class ExecutionLeases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "execution_leases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                WorkTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                OperatorSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                ExecutionSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                WorktreePath = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                BranchName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                Status = table.Column<int>(type: "INTEGER", nullable: false),
                RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                AllowedPathsJson = table.Column<string>(type: "TEXT", nullable: false),
                ForbiddenPathsJson = table.Column<string>(type: "TEXT", nullable: false),
                HandoffPacketJson = table.Column<string>(type: "TEXT", nullable: false),
                VerificationReportJson = table.Column<string>(type: "TEXT", nullable: true),
                EvidenceLogJson = table.Column<string>(type: "TEXT", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                MergedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_leases", x => x.Id);
                table.ForeignKey(
                    name: "FK_execution_leases_work_tasks_WorkTaskId",
                    column: x => x.WorkTaskId,
                    principalTable: "work_tasks",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_execution_leases_WorkTaskId",
            table: "execution_leases",
            column: "WorkTaskId");

        migrationBuilder.CreateIndex(
            name: "IX_execution_leases_WorkTaskId_Status",
            table: "execution_leases",
            columns: new[] { "WorkTaskId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "execution_leases");
    }
}
