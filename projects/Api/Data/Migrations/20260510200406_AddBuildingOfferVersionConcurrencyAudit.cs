using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingOfferVersionConcurrencyAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OfferVersion",
                table: "BuildingSaleOffers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "BuildingOfferSecurityAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpectedOfferVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    ActualOfferVersion = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuildingOfferSecurityAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingOfferSecurityAuditLogs_BuyerPlayerId_OccurredAtUtc",
                table: "BuildingOfferSecurityAuditLogs",
                columns: new[] { "BuyerPlayerId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BuildingOfferSecurityAuditLogs_OfferId_OccurredAtUtc",
                table: "BuildingOfferSecurityAuditLogs",
                columns: new[] { "OfferId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BuildingOfferSecurityAuditLogs");

            migrationBuilder.DropColumn(
                name: "OfferVersion",
                table: "BuildingSaleOffers");
        }
    }
}
