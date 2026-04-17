using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Api.Data;

/// <summary>
/// Design-time factory used by EF Core tooling (e.g. dotnet-ef migrations) to create
/// an <see cref="AppDbContext"/> instance with the same PostgreSQL provider used at runtime.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveConfigurationBasePath())
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("GameCatalog")
            ?? throw new InvalidOperationException("Connection string 'GameCatalog' is missing for design-time DbContext creation.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveConfigurationBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(System.IO.Path.Combine(currentDirectory, "appsettings.json")))
        {
            return currentDirectory;
        }

        var apiProjectDirectory = System.IO.Path.Combine(currentDirectory, "Api");
        if (File.Exists(System.IO.Path.Combine(apiProjectDirectory, "appsettings.json")))
        {
            return apiProjectDirectory;
        }

        throw new InvalidOperationException("Could not locate Api/appsettings.json for design-time DbContext creation.");
    }
}
