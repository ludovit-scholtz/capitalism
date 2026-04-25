using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairLegacyBooleanColumnTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    target record;
                BEGIN
                    FOR target IN
                        SELECT *
                        FROM (VALUES
                            ('Players', 'IsInvisibleInChat'),
                            ('Buildings', 'IsUnderConstruction'),
                            ('Buildings', 'BaseCapitalDeposited'),
                            ('BankDeposits', 'IsBaseCapital'),
                            ('BankDeposits', 'IsActive')
                        ) AS t(table_name, column_name)
                    LOOP
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns c
                            WHERE c.table_schema = 'public'
                              AND c.table_name = target.table_name
                              AND c.column_name = target.column_name
                              AND c.data_type <> 'boolean') THEN

                            EXECUTE format(
                                'ALTER TABLE "%I" ALTER COLUMN "%I" DROP DEFAULT',
                                target.table_name,
                                target.column_name);

                            EXECUTE format(
                                'ALTER TABLE "%I" ALTER COLUMN "%I" TYPE boolean USING CASE
                                    WHEN "%I" IS NULL THEN NULL
                                    WHEN lower(trim("%I"::text)) IN (''1'',''true'',''t'',''yes'',''y'') THEN TRUE
                                    WHEN lower(trim("%I"::text)) IN (''0'',''false'',''f'',''no'',''n'') THEN FALSE
                                    ELSE FALSE
                                 END',
                                target.table_name,
                                target.column_name,
                                target.column_name,
                                target.column_name,
                                target.column_name);
                        END IF;
                    END LOOP;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns c
                        WHERE c.table_schema = 'public'
                          AND c.table_name = 'Players'
                          AND c.column_name = 'IsInvisibleInChat') THEN
                        EXECUTE 'ALTER TABLE "Players" ALTER COLUMN "IsInvisibleInChat" SET DEFAULT FALSE';
                        EXECUTE 'UPDATE "Players" SET "IsInvisibleInChat" = FALSE WHERE "IsInvisibleInChat" IS NULL';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns c
                        WHERE c.table_schema = 'public'
                          AND c.table_name = 'Buildings'
                          AND c.column_name = 'IsUnderConstruction') THEN
                        EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "IsUnderConstruction" SET DEFAULT FALSE';
                        EXECUTE 'UPDATE "Buildings" SET "IsUnderConstruction" = FALSE WHERE "IsUnderConstruction" IS NULL';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns c
                        WHERE c.table_schema = 'public'
                          AND c.table_name = 'Buildings'
                          AND c.column_name = 'BaseCapitalDeposited') THEN
                        EXECUTE 'ALTER TABLE "Buildings" ALTER COLUMN "BaseCapitalDeposited" SET DEFAULT FALSE';
                        EXECUTE 'UPDATE "Buildings" SET "BaseCapitalDeposited" = FALSE WHERE "BaseCapitalDeposited" IS NULL';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: this migration repairs legacy schema drift in place.
        }
    }
}
