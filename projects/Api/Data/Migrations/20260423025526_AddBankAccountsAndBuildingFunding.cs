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
            if (ActiveProvider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                // BankAccounts.Id is ALWAYS UUID, regardless of Companies.Id type
                // Note: FK to Companies is NOT created here because Companies.Id may still be TEXT type
                // The FK will be added after ComprehensiveSchemaRepairWithCleanup migration converts Companies.Id to UUID
                migrationBuilder.Sql(
                    """
                    CREATE TABLE IF NOT EXISTS "BankAccounts" (
                        "Id" uuid NOT NULL,
                        "AccountNumber" character varying(16) NOT NULL,
                        "CurrencyCode" character varying(3) NOT NULL,
                        "Balance" numeric(18,2) NOT NULL,
                        "CompanyId" uuid NULL,
                        "IsGovernmentAccount" boolean NOT NULL,
                        "CreatedAtUtc" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_BankAccounts" PRIMARY KEY ("Id")
                    );
                    """);

                // Add BankAccountId column to Buildings as UUID
                migrationBuilder.Sql(
                    """
                    ALTER TABLE "Buildings" ADD COLUMN IF NOT EXISTS "BankAccountId" uuid NULL;
                    """);

                migrationBuilder.Sql("ALTER TABLE \"Buildings\" ADD COLUMN IF NOT EXISTS \"IsSuspendedForFunds\" boolean NOT NULL DEFAULT FALSE;");
                migrationBuilder.Sql("ALTER TABLE \"Buildings\" ADD COLUMN IF NOT EXISTS \"SuspendedReason\" character varying(200) NULL;");

                migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_BankAccounts_AccountNumber\" ON \"BankAccounts\" (\"AccountNumber\");");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CompanyId\" ON \"BankAccounts\" (\"CompanyId\");");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_BankAccounts_CurrencyCode_IsGovernmentAccount\" ON \"BankAccounts\" (\"CurrencyCode\", \"IsGovernmentAccount\");");
                migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS \"IX_Buildings_BankAccountId\" ON \"Buildings\" (\"BankAccountId\");");

                migrationBuilder.Sql(
                    """
                    DO $$
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM pg_constraint
                            WHERE conname = 'FK_Buildings_BankAccounts_BankAccountId') THEN
                            ALTER TABLE "Buildings"
                                ADD CONSTRAINT "FK_Buildings_BankAccounts_BankAccountId"
                                FOREIGN KEY ("BankAccountId") REFERENCES "BankAccounts" ("Id") ON DELETE SET NULL;
                        END IF;
                    END $$;
                    """);

                return;
            }

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
