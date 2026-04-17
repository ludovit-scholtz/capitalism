using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanCollateral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CollateralAppraisedValue",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CollateralBuildingId",
                table: "Loans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_CollateralBuildingId",
                table: "Loans",
                column: "CollateralBuildingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Loans_Buildings_CollateralBuildingId",
                table: "Loans",
                column: "CollateralBuildingId",
                principalTable: "Buildings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Loans_Buildings_CollateralBuildingId",
                table: "Loans");

            migrationBuilder.DropIndex(
                name: "IX_Loans_CollateralBuildingId",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CollateralAppraisedValue",
                table: "Loans");

            migrationBuilder.DropColumn(
                name: "CollateralBuildingId",
                table: "Loans");
        }
    }
}
