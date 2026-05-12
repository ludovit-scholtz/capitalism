using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"capitalism-tests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ApplyBaseConfiguration(builder);
    }

    protected void ApplyBaseConfiguration(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Unique per-factory InMemory database name ensures test-class isolation.
                ["ConnectionStrings:GameCatalog"] = _databaseName,
                ["SeedData:AdminEmail"] = "admin@capitalism.local",
                ["SeedData:AdminDisplayName"] = "Platform Admin",
                ["SeedData:AdminPassword"] = "ChangeMe123!",
                ["GameEngine:Enabled"] = "false",
                ["MasterServer:RegistrationEnabled"] = "false",
                ["Jwt:SigningKey"] = "TestingOnlyStrongSigningKey0123456789ABCDEF!",
                // Enable password auth in tests so all existing auth tests continue to pass.
                ["Auth:PasswordAuthEnabled"] = "true"
            });
        });
    }
}
