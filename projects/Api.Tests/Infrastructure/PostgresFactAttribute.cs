namespace Api.Tests.Infrastructure;

/// <summary>
/// A fact that needs a real PostgreSQL server. It runs only when <c>CAPITALISM_TEST_POSTGRES</c> holds a connection string
/// without a database name (for example <c>Host=localhost;Port=15432;Username=postgres;Password=...</c>); otherwise it is
/// skipped. CI provides the variable in <c>.github/workflows/postgres-validation.yml</c>.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "CAPITALISM_TEST_POSTGRES";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL connection string (no database name) to run PostgreSQL validation.";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(EnvironmentVariable);
}
