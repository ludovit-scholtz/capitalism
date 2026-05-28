using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasReceivedRegistrationEmail",
                table: "PlayerAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastAccessedUrl",
                table: "PlayerAccounts",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastWeeklyEmailSentAtUtc",
                table: "PlayerAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreferredLocale",
                table: "PlayerAccounts",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "en");

            migrationBuilder.AddColumn<DateTime>(
                name: "PreferredLocaleUpdatedAtUtc",
                table: "PlayerAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RegistrationEmailSentAtUtc",
                table: "PlayerAccounts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasReceivedRegistrationEmail",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "LastAccessedUrl",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "LastWeeklyEmailSentAtUtc",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "PreferredLocale",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "PreferredLocaleUpdatedAtUtc",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "RegistrationEmailSentAtUtc",
                table: "PlayerAccounts");
        }
    }
}
