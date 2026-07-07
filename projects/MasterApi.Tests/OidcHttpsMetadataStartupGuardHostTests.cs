using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class MasterOidcHttpsMetadataStartupGuardHostTests
{
    [Fact]
    public void Startup_Production_WithOidcEnabledAndRequireHttpsMetadataFalse_ThrowsInvalidOperation()
    {
        using var factory = new MasterOidcHttpsMetadataGuardFactory(
            "Production",
            oidcEnabled: true,
            requireHttpsMetadata: false,
            authority: "https://google.biatec.io");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("BiatecOidc:RequireHttpsMetadata", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BiatecOidc__RequireHttpsMetadata", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithOidcEnabledAndNonHttpsAuthority_ThrowsInvalidOperation()
    {
        using var factory = new MasterOidcHttpsMetadataGuardFactory(
            "Production",
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: "http://google.biatec.io");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("BiatecOidc:RequireHttpsMetadata", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Testing_WithOidcEnabledAndHttpsAuthority_DoesNotThrow()
    {
        // Uses the "Testing" environment (InMemory database) rather than "Production" so this
        // assertion does not depend on a reachable PostgreSQL instance; the guard treats
        // Development and Testing identically.
        using var factory = new MasterOidcHttpsMetadataGuardFactory(
            "Testing",
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: "https://google.biatec.io");

        using var _ = factory.CreateClient();
    }

    [Fact]
    public void Startup_Testing_WithOidcEnabledAndRequireHttpsMetadataFalse_DoesNotThrow()
    {
        using var factory = new MasterOidcHttpsMetadataGuardFactory(
            "Testing",
            oidcEnabled: true,
            requireHttpsMetadata: false,
            authority: "http://google.biatec.io");

        using var _ = factory.CreateClient();
    }
}

internal sealed class MasterOidcHttpsMetadataGuardFactory(
    string environmentName,
    bool oidcEnabled,
    bool requireHttpsMetadata,
    string authority)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var masterCatalogConnectionString =
            "Host=localhost;Port=5432;Database=master-guard;Username=postgres;Password=not-used-in-guard";

        builder.UseEnvironment(environmentName);
        builder.UseSetting("Jwt:SigningKey", "ProductionStrongSigningKey0123456789ABCDE!");
        builder.UseSetting("ConnectionStrings:MasterCatalog", masterCatalogConnectionString);
        builder.UseSetting("GameAdministration:RootAdministratorEmails:0", "root@example.com");
        builder.UseSetting("Auth:PasswordAuthEnabled", "true");
        builder.UseSetting("BiatecOidc:Enabled", oidcEnabled.ToString());
        builder.UseSetting("BiatecOidc:RequireHttpsMetadata", requireHttpsMetadata.ToString());
        builder.UseSetting("BiatecOidc:Authority", authority);
        builder.UseSetting("BiatecOidc:Audience", "capitalism");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterCatalog"] = masterCatalogConnectionString,
                ["MasterServer:RegistrationKey"] = "test-registration-key",
                ["MasterServer:ActiveThresholdSeconds"] = "90",
                ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
                ["Auth:PasswordAuthEnabled"] = "true",
                ["BiatecOidc:Enabled"] = oidcEnabled.ToString(),
                ["BiatecOidc:RequireHttpsMetadata"] = requireHttpsMetadata.ToString(),
                ["BiatecOidc:Authority"] = authority,
                ["BiatecOidc:Audience"] = "capitalism",
            });
        });
    }
}
