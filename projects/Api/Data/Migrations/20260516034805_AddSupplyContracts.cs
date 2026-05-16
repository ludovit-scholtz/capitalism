using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplyContracts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GovernmentContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequired = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MinimumQuality = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    BudgetCap = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DeadlineTick = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WinnerCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeadlineWarningSentAtTick = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GovernmentContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GovernmentContracts_Cities_CityId",
                        column: x => x.CityId,
                        principalTable: "Cities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GovernmentContracts_Companies_WinnerCompanyId",
                        column: x => x.WinnerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GovernmentContracts_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupplyContracts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BuyerCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerBuildingUnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityPerTick = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PricePerUnit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DurationTicks = table.Column<int>(type: "integer", nullable: false),
                    RemainingTicks = table.Column<int>(type: "integer", nullable: false),
                    StartTick = table.Column<long>(type: "bigint", nullable: false),
                    PenaltyRatePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActivatedAtTick = table.Column<long>(type: "bigint", nullable: true),
                    ActivatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtTick = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAtTick = table.Column<long>(type: "bigint", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalDeliveredQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalUndeliveredQuantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalPenaltyAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    PenaltyCount = table.Column<int>(type: "integer", nullable: false),
                    FirstDeliveryNotified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplyContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplyContracts_BuildingUnits_SellerBuildingUnitId",
                        column: x => x.SellerBuildingUnitId,
                        principalTable: "BuildingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyContracts_Companies_BuyerCompanyId",
                        column: x => x.BuyerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyContracts_Companies_SellerCompanyId",
                        column: x => x.SellerCompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyContracts_ProductTypes_ProductTypeId",
                        column: x => x.ProductTypeId,
                        principalTable: "ProductTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplyContracts_ResourceTypes_ResourceTypeId",
                        column: x => x.ResourceTypeId,
                        principalTable: "ResourceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContractBids",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BidPricePerUnit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EstimatedDeliveryTick = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractBids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractBids_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractBids_GovernmentContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "GovernmentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractFulfillments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContractId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityDelivered = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantityRequired = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    LastShipmentTick = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractFulfillments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractFulfillments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ContractFulfillments_GovernmentContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "GovernmentContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractBids_CompanyId",
                table: "ContractBids",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractBids_ContractId_BidPricePerUnit",
                table: "ContractBids",
                columns: new[] { "ContractId", "BidPricePerUnit" });

            migrationBuilder.CreateIndex(
                name: "IX_ContractBids_ContractId_CompanyId",
                table: "ContractBids",
                columns: new[] { "ContractId", "CompanyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContractFulfillments_CompanyId",
                table: "ContractFulfillments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractFulfillments_ContractId",
                table: "ContractFulfillments",
                column: "ContractId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContracts_CityId_Status_DeadlineTick",
                table: "GovernmentContracts",
                columns: new[] { "CityId", "Status", "DeadlineTick" });

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContracts_ProductTypeId",
                table: "GovernmentContracts",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GovernmentContracts_WinnerCompanyId_Status",
                table: "GovernmentContracts",
                columns: new[] { "WinnerCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_BuyerCompanyId_Status",
                table: "SupplyContracts",
                columns: new[] { "BuyerCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_ProductTypeId",
                table: "SupplyContracts",
                column: "ProductTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_ResourceTypeId",
                table: "SupplyContracts",
                column: "ResourceTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_SellerBuildingUnitId",
                table: "SupplyContracts",
                column: "SellerBuildingUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_SellerCompanyId_Status",
                table: "SupplyContracts",
                columns: new[] { "SellerCompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupplyContracts_Status_StartTick",
                table: "SupplyContracts",
                columns: new[] { "Status", "StartTick" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractBids");

            migrationBuilder.DropTable(
                name: "ContractFulfillments");

            migrationBuilder.DropTable(
                name: "SupplyContracts");

            migrationBuilder.DropTable(
                name: "GovernmentContracts");
        }
    }
}
