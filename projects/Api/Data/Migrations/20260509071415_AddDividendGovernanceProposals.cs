using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDividendGovernanceProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DividendProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockSymbol = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProposedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedByAccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DividendPerShare = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalPayout = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProposedAtTick = table.Column<long>(type: "bigint", nullable: false),
                    VotingOpenTick = table.Column<long>(type: "bigint", nullable: false),
                    VotingCloseTick = table.Column<long>(type: "bigint", nullable: false),
                    SettledAtTick = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SettledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DividendProposals_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DividendVotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterAccountType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SharesVoted = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    VoteChoice = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    CastAtTick = table.Column<long>(type: "bigint", nullable: false),
                    CastAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DividendVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DividendVotes_DividendProposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "DividendProposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DividendProposals_CompanyId_Status_VotingCloseTick",
                table: "DividendProposals",
                columns: new[] { "CompanyId", "Status", "VotingCloseTick" });

            migrationBuilder.CreateIndex(
                name: "IX_DividendProposals_StockSymbol_ProposedAtTick",
                table: "DividendProposals",
                columns: new[] { "StockSymbol", "ProposedAtTick" });

            migrationBuilder.CreateIndex(
                name: "IX_DividendVotes_ProposalId_VoteChoice",
                table: "DividendVotes",
                columns: new[] { "ProposalId", "VoteChoice" });

            migrationBuilder.CreateIndex(
                name: "IX_DividendVotes_ProposalId_VoterAccountId",
                table: "DividendVotes",
                columns: new[] { "ProposalId", "VoterAccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DividendVotes");

            migrationBuilder.DropTable(
                name: "DividendProposals");
        }
    }
}
