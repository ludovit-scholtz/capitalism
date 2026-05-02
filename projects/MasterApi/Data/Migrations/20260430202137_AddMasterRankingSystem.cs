using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MasterApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterRankingSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MasterRankingBountyDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RewardPoints = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisibleToPlayers = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresModeration = table.Column<bool>(type: "boolean", nullable: false),
                    CooldownMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceEventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProofRequirement = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ValidationSettingsJson = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    VisibilityScope = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingBountyDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterRankingEvaluationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedEvents = table.Column<int>(type: "integer", nullable: false),
                    RewardRecordsCreated = table.Column<int>(type: "integer", nullable: false),
                    TotalPointsAwarded = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalPointsBeforeDecay = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalPointsAfterDecay = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingEvaluationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MasterRankingEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlayerEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ServerKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExternalEventId = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    UniqueScopeKey = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true),
                    PayloadJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    ProofReference = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModerationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ModeratedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ModeratedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterRankingEvents_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MasterRankingPlayerSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    GlobalRank = table.Column<int>(type: "integer", nullable: false),
                    PreviousGlobalRank = table.Column<int>(type: "integer", nullable: false),
                    LastDailyDecayFactorApplied = table.Column<decimal>(type: "numeric(9,6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingPlayerSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterRankingPlayerSnapshots_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MasterRankingBountyAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BountyDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedByEmail = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreviousValueJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    NewValueJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingBountyAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterRankingBountyAudits_MasterRankingBountyDefinitions_Bo~",
                        column: x => x.BountyDefinitionId,
                        principalTable: "MasterRankingBountyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MasterRankingRewardRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    BountyDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RankingEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    PointsAwarded = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UniquenessKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    ServerKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    EventDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AwardMetadataJson = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MasterRankingRewardRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MasterRankingRewardRecords_MasterRankingBountyDefinitions_B~",
                        column: x => x.BountyDefinitionId,
                        principalTable: "MasterRankingBountyDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MasterRankingRewardRecords_MasterRankingEvents_RankingEvent~",
                        column: x => x.RankingEventId,
                        principalTable: "MasterRankingEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MasterRankingRewardRecords_PlayerAccounts_PlayerAccountId",
                        column: x => x.PlayerAccountId,
                        principalTable: "PlayerAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingBountyAudits_BountyDefinitionId_CreatedAtUtc",
                table: "MasterRankingBountyAudits",
                columns: new[] { "BountyDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingBountyDefinitions_Code",
                table: "MasterRankingBountyDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvaluationRuns_RunType_StartedAtUtc",
                table: "MasterRankingEvaluationRuns",
                columns: new[] { "RunType", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvents_EventType_Status_CreatedAtUtc",
                table: "MasterRankingEvents",
                columns: new[] { "EventType", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvents_ExternalEventId",
                table: "MasterRankingEvents",
                column: "ExternalEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingEvents_PlayerAccountId",
                table: "MasterRankingEvents",
                column: "PlayerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingPlayerSnapshots_GlobalRank",
                table: "MasterRankingPlayerSnapshots",
                column: "GlobalRank");

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingPlayerSnapshots_PlayerAccountId",
                table: "MasterRankingPlayerSnapshots",
                column: "PlayerAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingRewardRecords_BountyDefinitionId",
                table: "MasterRankingRewardRecords",
                column: "BountyDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingRewardRecords_PlayerAccountId_AwardedAtUtc",
                table: "MasterRankingRewardRecords",
                columns: new[] { "PlayerAccountId", "AwardedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingRewardRecords_RankingEventId",
                table: "MasterRankingRewardRecords",
                column: "RankingEventId");

            migrationBuilder.CreateIndex(
                name: "IX_MasterRankingRewardRecords_UniquenessKey",
                table: "MasterRankingRewardRecords",
                column: "UniquenessKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MasterRankingBountyAudits");

            migrationBuilder.DropTable(
                name: "MasterRankingEvaluationRuns");

            migrationBuilder.DropTable(
                name: "MasterRankingPlayerSnapshots");

            migrationBuilder.DropTable(
                name: "MasterRankingRewardRecords");

            migrationBuilder.DropTable(
                name: "MasterRankingBountyDefinitions");

            migrationBuilder.DropTable(
                name: "MasterRankingEvents");
        }
    }
}
