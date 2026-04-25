using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Data.Migrations
{
    /// <summary>
    /// Repairs legacy PostgreSQL databases that still store Guid keys as text and then fail with
    /// "cannot be implemented" when later migrations add or validate foreign keys.
    /// </summary>
    public partial class NormalizeLegacyGuidColumnsForPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            if (!ActiveProvider.Contains("Npgsql"))
            {
                return;
            }

            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    fk RECORD;
                    col RECORD;
                    non_uuid_count BIGINT;
                    uuid_pattern CONSTANT TEXT := '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$';
                BEGIN
                    CREATE TEMP TABLE __tmp_fk_defs (
                        table_schema TEXT NOT NULL,
                        table_name TEXT NOT NULL,
                        constraint_name TEXT NOT NULL,
                        constraint_def TEXT NOT NULL
                    ) ON COMMIT DROP;

                    INSERT INTO __tmp_fk_defs (table_schema, table_name, constraint_name, constraint_def)
                    SELECT
                        ns.nspname,
                        tbl.relname,
                        con.conname,
                        pg_get_constraintdef(con.oid)
                    FROM pg_constraint con
                    JOIN pg_class tbl ON tbl.oid = con.conrelid
                    JOIN pg_namespace ns ON ns.oid = tbl.relnamespace
                    WHERE con.contype = 'f'
                      AND ns.nspname = 'public';

                    FOR fk IN SELECT * FROM __tmp_fk_defs LOOP
                        EXECUTE format(
                            'ALTER TABLE %I.%I DROP CONSTRAINT %I',
                            fk.table_schema,
                            fk.table_name,
                            fk.constraint_name);
                    END LOOP;

                    FOR col IN
                        SELECT c.table_schema, c.table_name, c.column_name
                        FROM information_schema.columns c
                        JOIN information_schema.tables t
                          ON t.table_schema = c.table_schema
                         AND t.table_name = c.table_name
                        WHERE c.table_schema = 'public'
                          AND t.table_type = 'BASE TABLE'
                          AND c.data_type IN ('text', 'character varying')
                          AND (c.column_name = 'Id' OR c.column_name LIKE '%Id')
                    LOOP
                        EXECUTE format(
                            'SELECT COUNT(1) FROM %I.%I WHERE %I IS NOT NULL AND NULLIF(%I::text, '''') IS NOT NULL AND %I::text !~* %L',
                            col.table_schema,
                            col.table_name,
                            col.column_name,
                            col.column_name,
                            col.column_name,
                            uuid_pattern)
                        INTO non_uuid_count;

                        IF non_uuid_count = 0 THEN
                            BEGIN
                                EXECUTE format(
                                    'ALTER TABLE %I.%I ALTER COLUMN %I DROP DEFAULT',
                                    col.table_schema,
                                    col.table_name,
                                    col.column_name);
                            EXCEPTION
                                WHEN OTHERS THEN
                                    NULL;
                            END;

                            EXECUTE format(
                                'ALTER TABLE %I.%I ALTER COLUMN %I TYPE uuid USING NULLIF(%I::text, '''')::uuid',
                                col.table_schema,
                                col.table_name,
                                col.column_name,
                                col.column_name);
                        END IF;
                    END LOOP;

                    FOR fk IN SELECT * FROM __tmp_fk_defs LOOP
                        EXECUTE format(
                            'ALTER TABLE %I.%I ADD CONSTRAINT %I %s',
                            fk.table_schema,
                            fk.table_name,
                            fk.constraint_name,
                            fk.constraint_def);
                    END LOOP;
                END $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible safety migration.
        }
    }
}
