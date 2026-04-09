using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MasterApi;

namespace MasterApi.Tests.Infrastructure;

public sealed class MasterApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"masterapi-tests-{Guid.NewGuid():N}.db")}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterCatalog"] = _connectionString,
                ["MasterServer:RegistrationKey"] = "test-registration-key",
                ["MasterServer:ActiveThresholdSeconds"] = "90",
            });
        });
    }
}
