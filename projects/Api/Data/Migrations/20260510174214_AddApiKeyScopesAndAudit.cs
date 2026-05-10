using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyScopesAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "CompanyIds",
                table: "PlayerApiKeys",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string[]>(
                name: "Scopes",
                table: "PlayerApiKeys",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateTable(
                name: "PlayerApiKeyAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerApiKeyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OperationType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ScopeUsed = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    WasAllowed = table.Column<bool>(type: "boolean", nullable: false),
                    DenialCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerApiKeyAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerApiKeyAuditLogs_PlayerApiKeys_PlayerApiKeyId",
                        column: x => x.PlayerApiKeyId,
                        principalTable: "PlayerApiKeys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerApiKeyAuditLogs_PlayerApiKeyId_OccurredAtUtc",
                table: "PlayerApiKeyAuditLogs",
                columns: new[] { "PlayerApiKeyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerApiKeyAuditLogs_PlayerId_OccurredAtUtc",
                table: "PlayerApiKeyAuditLogs",
                columns: new[] { "PlayerId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerApiKeyAuditLogs");

            migrationBuilder.DropColumn(
                name: "CompanyIds",
                table: "PlayerApiKeys");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "PlayerApiKeys");
        }
    }
}
