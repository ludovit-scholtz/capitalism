using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCityScopedChatChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE \"ChatMessages\" DROP CONSTRAINT IF EXISTS \"FK_ChatMessages_Players_PlayerId\";");

            migrationBuilder.RenameColumn(
                name: "SentAtUtc",
                table: "ChatMessages",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "PlayerId",
                table: "ChatMessages",
                newName: "AuthorPlayerId");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "ChatMessages",
                newName: "Content");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_SentAtUtc",
                table: "ChatMessages",
                newName: "IX_ChatMessages_CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_PlayerId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_AuthorPlayerId");

            migrationBuilder.AddColumn<string>(
                name: "AuthorDisplayName",
                table: "ChatMessages",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CityId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Content",
                table: "ChatMessages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<bool>(
                name: "IsVisible",
                table: "ChatMessages",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE "ChatMessages" AS m
                SET "AuthorDisplayName" = COALESCE(p."DisplayName", p."Email", 'Unknown')
                FROM "Players" AS p
                WHERE p."Id" = m."AuthorPlayerId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_CityId_CreatedAtUtc",
                table: "ChatMessages",
                columns: new[] { "CityId", "CreatedAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Cities_CityId",
                table: "ChatMessages",
                column: "CityId",
                principalTable: "Cities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Players_AuthorPlayerId",
                table: "ChatMessages",
                column: "AuthorPlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Cities_CityId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Players_AuthorPlayerId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_CityId_CreatedAtUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AuthorDisplayName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsVisible",
                table: "ChatMessages");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "ChatMessages",
                newName: "SentAtUtc");

            migrationBuilder.RenameColumn(
                name: "AuthorPlayerId",
                table: "ChatMessages",
                newName: "PlayerId");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "ChatMessages",
                newName: "Message");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_CreatedAtUtc",
                table: "ChatMessages",
                newName: "IX_ChatMessages_SentAtUtc");

            migrationBuilder.RenameIndex(
                name: "IX_ChatMessages_AuthorPlayerId",
                table: "ChatMessages",
                newName: "IX_ChatMessages_PlayerId");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "ChatMessages",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Players_PlayerId",
                table: "ChatMessages",
                column: "PlayerId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
