using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenLoanCollateralCommitRevalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "Loans",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyToken",
                table: "Buildings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "LoanCollateralSecurityAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoanId = table.Column<Guid>(type: "uuid", nullable: true),
                    BuildingId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeadLetter = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanCollateralSecurityAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoanCollateralSecurityAuditLogs_BuildingId_OccurredAtUtc",
                table: "LoanCollateralSecurityAuditLogs",
                columns: new[] { "BuildingId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanCollateralSecurityAuditLogs_LoanId_OccurredAtUtc",
                table: "LoanCollateralSecurityAuditLogs",
                columns: new[] { "LoanId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanCollateralSecurityAuditLogs_PlayerId_OccurredAtUtc",
                table: "LoanCollateralSecurityAuditLogs",
                columns: new[] { "PlayerId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanCollateralSecurityAuditLogs");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "ConcurrencyToken",
                table: "Buildings");
        }
    }
}
