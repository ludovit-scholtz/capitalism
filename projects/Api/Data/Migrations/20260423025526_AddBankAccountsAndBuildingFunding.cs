using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBankAccountsAndBuildingFunding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsGovernmentAccount = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "BankAccountId",
                table: "Buildings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuspendedForFunds",
                table: "Buildings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SuspendedReason",
                table: "Buildings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_BankAccountId",
                table: "Buildings",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_AccountNumber",
                table: "BankAccounts",
                column: "AccountNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId",
                table: "BankAccounts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CurrencyCode_IsGovernmentAccount",
                table: "BankAccounts",
                columns: new[] { "CurrencyCode", "IsGovernmentAccount" });

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_BankAccounts_BankAccountId",
                table: "Buildings",
                column: "BankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_BankAccounts_BankAccountId",
                table: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_BankAccountId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "BankAccountId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "IsSuspendedForFunds",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "SuspendedReason",
                table: "Buildings");

            migrationBuilder.DropTable(
                name: "BankAccounts");
        }
    }
}
