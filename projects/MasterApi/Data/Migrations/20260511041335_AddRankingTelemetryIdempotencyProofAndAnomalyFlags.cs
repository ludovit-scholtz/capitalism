using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRankingTelemetryIdempotencyProofAndAnomalyFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "MasterRankingEvents",
                type: "character varying(220)",
                maxLength: 220,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvents_PlayerEmail_EventType_ServerKey_Idempot~",
                table: "MasterRankingEvents",
                columns: new[] { "PlayerEmail", "EventType", "ServerKey", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvents_ProofReference",
                table: "MasterRankingEvents",
                column: "ProofReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MasterRankingEvents_PlayerEmail_EventType_ServerKey_Idempot~",
                table: "MasterRankingEvents");

            migrationBuilder.DropIndex(
                name: "IX_MasterRankingEvents_ProofReference",
                table: "MasterRankingEvents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "MasterRankingEvents");
        }
    }
}
