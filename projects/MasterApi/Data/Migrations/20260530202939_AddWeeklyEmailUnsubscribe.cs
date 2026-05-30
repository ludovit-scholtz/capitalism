using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyEmailUnsubscribe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EmailUnsubscribeToken",
                table: "PlayerAccounts",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<bool>(
                name: "WeeklyReportEmailUnsubscribed",
                table: "PlayerAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerAccounts_EmailUnsubscribeToken",
                table: "PlayerAccounts",
                column: "EmailUnsubscribeToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerAccounts_EmailUnsubscribeToken",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "EmailUnsubscribeToken",
                table: "PlayerAccounts");

            migrationBuilder.DropColumn(
                name: "WeeklyReportEmailUnsubscribed",
                table: "PlayerAccounts");
        }
    }
}
