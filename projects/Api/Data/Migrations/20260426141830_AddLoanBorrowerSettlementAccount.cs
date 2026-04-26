using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanBorrowerSettlementAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BorrowerBankAccountId",
                table: "Loans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_BorrowerBankAccountId",
                table: "Loans",
                column: "BorrowerBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_BankAccounts_BorrowerBankAccountId",
                table: "Loans",
                column: "BorrowerBankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loans_BankAccounts_BorrowerBankAccountId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_BorrowerBankAccountId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "BorrowerBankAccountId",
                table: "Loans");
        }
    }
}
