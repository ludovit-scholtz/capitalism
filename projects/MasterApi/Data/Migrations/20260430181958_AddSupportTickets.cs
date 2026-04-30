using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByPlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TicketType = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Title = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    MarkdownSource = table.Column<string>(type: "text", nullable: false),
                    SanitizedPreviewHtml = table.Column<string>(type: "text", nullable: true),
                    ExtractedUrlsJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ExtractedImagesJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ContainsUnsafeContent = table.Column<bool>(type: "boolean", nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ModerationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ModeratedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModeratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StatusUpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_PlayerAccounts_CreatedByPlayerAccountId",
                        column: x => x.CreatedByPlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupportTicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActorDisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketAuditEvents_SupportTickets_SupportTicketId",
                        column: x => x.SupportTicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketAuditEvents_SupportTicketId_CreatedAtUtc",
                table: "SupportTicketAuditEvents",
                columns: new[] { "SupportTicketId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedByPlayerAccountId_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "CreatedByPlayerAccountId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status_UpdatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TicketType_CreatedAtUtc",
                table: "SupportTickets",
                columns: new[] { "TicketType", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportTicketAuditEvents");

            migrationBuilder.DropTable(
                name: "SupportTickets");
        }
    }
}
