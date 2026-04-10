using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using MasterApi;

namespace MasterApi.Tests.Infrastructure;

public sealed class MasterApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName;

    public MasterApiWebApplicationFactory()
        : this($"masterapi-tests-{Guid.NewGuid():N}")
    {
    }

    internal MasterApiWebApplicationFactory(string databaseName)
    {
        _databaseName = databaseName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterCatalog"] = _databaseName,
                ["MasterServer:RegistrationKey"] = "test-registration-key",
                ["MasterServer:ActiveThresholdSeconds"] = "90",
                ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
            });
        });
    }
}
