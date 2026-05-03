using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandSeasonality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandSeasonalities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Q1Multiplier = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    Q2Multiplier = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    Q3Multiplier = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false),
                    Q4Multiplier = table.Column<decimal>(type: "numeric(5,3)", precision: 5, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandSeasonalities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandSeasonalities_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DemandSeasonalities_ProductTypeId",
                table: "DemandSeasonalities",
                column: "ProductTypeId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandSeasonalities");
        }
    }
}
