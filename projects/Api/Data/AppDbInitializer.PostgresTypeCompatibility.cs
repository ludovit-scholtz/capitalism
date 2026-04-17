using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Api.Data;

/// <summary>
/// Repairs legacy PostgreSQL schemas that were created from SQLite-scaffolded migrations
/// and therefore stored GUIDs/decimals/timestamps/booleans in incompatible column types.
/// </summary>
public sealed partial class AppDbInitializer
{
    private async Task RepairLegacyPostgresStoreTypesAsync(DbConnection connection, SchemaDialect dialect)
    {
        if (!dialect.IsPostgres)
        {
            return;
        }

        var conversions = await BuildPostgresColumnConversionsAsync(connection);
        if (conversions.Count == 0)
        {
            return;
        }

        var foreignKeys = await LoadPostgresForeignKeysAsync(connection);

        foreach (var foreignKey in foreignKeys)
        {
            await ExecuteNonQueryAsync(
                connection,
                $"ALTER TABLE \"{foreignKey.TableName}\" DROP CONSTRAINT IF EXISTS \"{foreignKey.ConstraintName}\"");
        }

        foreach (var conversion in conversions)
        {
            await ExecuteNonQueryAsync(connection, conversion.Sql);
        }

        foreach (var foreignKey in foreignKeys)
        {
            await ExecuteNonQueryAsync(
                connection,
                $"ALTER TABLE \"{foreignKey.TableName}\" ADD CONSTRAINT \"{foreignKey.ConstraintName}\" {foreignKey.ConstraintDefinition}");
        }
    }

    private async Task<List<PostgresColumnConversion>> BuildPostgresColumnConversionsAsync(DbConnection connection)
    {
        var currentColumns = await LoadPostgresColumnStoreTypesAsync(connection);
        var conversions = new List<PostgresColumnConversion>();

        foreach (var table in dbContext.Model.GetRelationalModel().Tables)
        {
            if (table.Schema is not null && !string.Equals(table.Schema, "public", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var column in table.Columns)
            {
                if (!currentColumns.TryGetValue(
                        (NormalizeIdentifier(table.Name), NormalizeIdentifier(column.Name)),
                        out var currentStoreType))
                {
                    continue;
                }

                var expectedStoreType = NormalizePostgresStoreType(column.StoreType);
                if (!TryBuildPostgresColumnConversionSql(
                        table.Name,
                        column.Name,
                        currentStoreType,
                        expectedStoreType,
                        column.IsNullable,
                        out var sql))
                {
                    continue;
                }

                conversions.Add(new PostgresColumnConversion(table.Name, column.Name, currentStoreType, expectedStoreType, sql));
            }
        }

        return conversions;
    }

    private static async Task<Dictionary<(string TableName, string ColumnName), string>> LoadPostgresColumnStoreTypesAsync(DbConnection connection)
    {
        var result = new Dictionary<(string TableName, string ColumnName), string>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT c.relname AS table_name,
                   a.attname AS column_name,
                   pg_catalog.format_type(a.atttypid, a.atttypmod) AS store_type
            FROM pg_attribute a
            JOIN pg_class c ON c.oid = a.attrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public'
              AND c.relkind = 'r'
              AND a.attnum > 0
              AND NOT a.attisdropped
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var storeType = NormalizePostgresStoreType(reader.GetString(2));
            result[(NormalizeIdentifier(tableName), NormalizeIdentifier(columnName))] = storeType;
        }

        return result;
    }

    private static async Task<List<PostgresForeignKeyDefinition>> LoadPostgresForeignKeysAsync(DbConnection connection)
    {
        var result = new List<PostgresForeignKeyDefinition>();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT rel.relname AS table_name,
                   con.conname AS constraint_name,
                   pg_get_constraintdef(con.oid) AS constraint_definition
            FROM pg_constraint con
            JOIN pg_class rel ON rel.oid = con.conrelid
            JOIN pg_namespace n ON n.oid = rel.relnamespace
            WHERE con.contype = 'f'
              AND n.nspname = 'public'
            ORDER BY rel.relname, con.conname
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new PostgresForeignKeyDefinition(
                TableName: reader.GetString(0),
                ConstraintName: reader.GetString(1),
                ConstraintDefinition: reader.GetString(2)));
        }

        return result;
    }

    private static bool TryBuildPostgresColumnConversionSql(
        string tableName,
        string columnName,
        string currentStoreType,
        string expectedStoreType,
        bool isNullable,
        out string sql)
    {
        sql = string.Empty;

        if (string.Equals(currentStoreType, expectedStoreType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var quotedColumn = $"\"{columnName}\"";
        string? usingExpression = (currentStoreType, expectedStoreType) switch
        {
            ("text", "uuid") => isNullable ? $"NULLIF({quotedColumn}, '')::uuid" : $"{quotedColumn}::uuid",
            ("text", var expected) when expected.StartsWith("numeric", StringComparison.Ordinal) =>
                isNullable ? $"NULLIF({quotedColumn}, '')::{expected}" : $"{quotedColumn}::{expected}",
            ("text", "timestamp with time zone") =>
                isNullable ? $"NULLIF({quotedColumn}, '')::timestamp with time zone" : $"{quotedColumn}::timestamp with time zone",
            ("text", "bigint") =>
                isNullable ? $"NULLIF({quotedColumn}, '')::bigint" : $"{quotedColumn}::bigint",
            ("text", "boolean") => $"CASE WHEN LOWER(COALESCE({quotedColumn}, '')) IN ('1', 't', 'true') THEN TRUE ELSE FALSE END",
            ("integer", "boolean") => $"CASE WHEN {quotedColumn} = 0 THEN FALSE ELSE TRUE END",
            ("integer", "bigint") => $"{quotedColumn}::bigint",
            ("real", "double precision") => $"{quotedColumn}::double precision",
            _ => null,
        };

        if (usingExpression is null)
        {
            return false;
        }

        sql =
            $"ALTER TABLE \"{tableName}\" ALTER COLUMN {quotedColumn} TYPE {expectedStoreType} USING {usingExpression}";
        return true;
    }

    private static string NormalizePostgresStoreType(string storeType) =>
        string.Join(' ', storeType.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string NormalizeIdentifier(string identifier) => identifier.Trim().ToLowerInvariant();

    private sealed record PostgresColumnConversion(
        string TableName,
        string ColumnName,
        string CurrentStoreType,
        string ExpectedStoreType,
        string Sql);

    private sealed record PostgresForeignKeyDefinition(
        string TableName,
        string ConstraintName,
        string ConstraintDefinition);
}
