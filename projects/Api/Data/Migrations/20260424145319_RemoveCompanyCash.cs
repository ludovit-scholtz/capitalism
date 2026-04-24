using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCompanyCash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cash",
                table: "Companies");

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId1",
                table: "BankAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId1",
                table: "BankAccounts",
                column: "CompanyId1");

            migrationBuilder.AddForeignKey(
                name: "FK_BankAccounts_Companies_CompanyId1",
                table: "BankAccounts",
                column: "CompanyId1",
                principalTable: "Companies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankAccounts_Companies_CompanyId1",
                table: "BankAccounts");

            migrationBuilder.DropIndex(
                name: "IX_BankAccounts_CompanyId1",
                table: "BankAccounts");

            migrationBuilder.DropColumn(
                name: "CompanyId1",
                table: "BankAccounts");

            migrationBuilder.AddColumn<decimal>(
                name: "Cash",
                table: "Companies",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
