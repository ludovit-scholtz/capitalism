using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyOwnershipRejectionAuditContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttemptedObjectId",
                table: "PlayerApiKeyAuditLogs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DenialReason",
                table: "PlayerApiKeyAuditLogs",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionContext",
                table: "PlayerApiKeyAuditLogs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptedObjectId",
                table: "PlayerApiKeyAuditLogs");

            migrationBuilder.DropColumn(
                name: "DenialReason",
                table: "PlayerApiKeyAuditLogs");

            migrationBuilder.DropColumn(
                name: "SessionContext",
                table: "PlayerApiKeyAuditLogs");
        }
    }
}
