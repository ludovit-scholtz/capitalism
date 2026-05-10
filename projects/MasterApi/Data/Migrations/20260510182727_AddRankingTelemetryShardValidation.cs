using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingTelemetryShardValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuarantined",
                table: "MasterRankingEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PayloadHash",
                table: "MasterRankingEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuarantineClearJustification",
                table: "MasterRankingEvents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuarantineClearedAtUtc",
                table: "MasterRankingEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuarantineClearedByEmail",
                table: "MasterRankingEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuarantineReason",
                table: "MasterRankingEvents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QuarantinedAtUtc",
                table: "MasterRankingEvents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuarantinedByEmail",
                table: "MasterRankingEvents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServerKeyHash",
                table: "MasterRankingEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TelemetryBatchId",
                table: "MasterRankingEvents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelemetryNonce",
                table: "MasterRankingEvents",
                type: "character varying(220)",
                maxLength: 220,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TelemetrySignatureHash",
                table: "MasterRankingEvents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "GameServers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "GameServers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ServerKeyHash",
                table: "GameServers",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RankingTelemetryAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerKeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ServerKeyMasked = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PlayerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventNonce = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RawPayloadJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    IsRejected = table.Column<bool>(type: "boolean", nullable: false),
                    IsQuarantined = table.Column<bool>(type: "boolean", nullable: false),
                    QuarantineReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuarantineUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    QuarantineUpdatedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClearJustification = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingTelemetryAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RankingTelemetryEventSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SignatureHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RankingTelemetryEventSignatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameServers_ServerKeyHash",
                table: "GameServers",
                column: "ServerKeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RankingTelemetryAuditLogs_BatchId_CreatedAtUtc",
                table: "RankingTelemetryAuditLogs",
                columns: new[] { "BatchId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RankingTelemetryAuditLogs_ReasonCode",
                table: "RankingTelemetryAuditLogs",
                column: "ReasonCode");

            migrationBuilder.CreateIndex(
                name: "IX_RankingTelemetryEventSignatures_ExpiresAtUtc",
                table: "RankingTelemetryEventSignatures",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RankingTelemetryEventSignatures_SignatureHash",
                table: "RankingTelemetryEventSignatures",
                column: "SignatureHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RankingTelemetryAuditLogs");

            migrationBuilder.DropTable(
                name: "RankingTelemetryEventSignatures");

            migrationBuilder.DropIndex(
                name: "IX_GameServers_ServerKeyHash",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "IsQuarantined",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "PayloadHash",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantineClearJustification",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantineClearedAtUtc",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantineClearedByEmail",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantineReason",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantinedAtUtc",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "QuarantinedByEmail",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "ServerKeyHash",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "TelemetryBatchId",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "TelemetryNonce",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "TelemetrySignatureHash",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "GameServers");

            migrationBuilder.DropColumn(
                name: "ServerKeyHash",
                table: "GameServers");
        }
    }
}
