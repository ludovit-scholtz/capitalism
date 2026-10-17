namespace Api.Tests.Infrastructure;

/// <summary>
/// A fact that needs a real PostgreSQL server. It runs only when <c>CAPITALISM_TEST_POSTGRES</c> holds a connection string
/// without a database name (for example <c>Host=localhost;Port=15432;Username=postgres;Password=...</c>); otherwise it is
/// skipped. CI provides the variable in <c>.github/workflows/postgres-validation.yml</c>.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "CAPITALISM_TEST_POSTGRES";

    /// <summary>
    /// When set to "true" (CI), a missing connection string is a failure instead of a skip, so a misspelled variable can never turn
    /// the PostgreSQL job green without validating anything.
    /// </summary>
    public const string RequiredEnvironmentVariable = "CAPITALISM_TEST_POSTGRES_REQUIRED";

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable))
            && !string.Equals(Environment.GetEnvironmentVariable(RequiredEnvironmentVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL connection string (no database name) to run PostgreSQL validation.";
        }
    }

    public static string ConnectionString
        => Environment.GetEnvironmentVariable(EnvironmentVariable)
           ?? throw new InvalidOperationException($"{EnvironmentVariable} is required for the PostgreSQL validation tests but is not set.");
}
