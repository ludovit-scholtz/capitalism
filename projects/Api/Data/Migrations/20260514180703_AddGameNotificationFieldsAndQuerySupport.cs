using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNotificationFieldsAndQuerySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyKey",
                table: "PlayerNotifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BodyParamsJson",
                table: "PlayerNotifications",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "PlayerNotifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "PlayerNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityType",
                table: "PlayerNotifications",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "PlayerNotifications",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "INFO");

            migrationBuilder.AddColumn<string>(
                name: "TitleKey",
                table: "PlayerNotifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNotifications_PlayerId_ExpiresAtUtc",
                table: "PlayerNotifications",
                columns: new[] { "PlayerId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerNotifications_PlayerId_ExpiresAtUtc",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "BodyKey",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "BodyParamsJson",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "RelatedEntityType",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "PlayerNotifications");

            migrationBuilder.DropColumn(
                name: "TitleKey",
                table: "PlayerNotifications");
        }
    }
}
