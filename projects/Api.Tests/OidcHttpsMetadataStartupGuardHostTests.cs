using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

public sealed class OidcHttpsMetadataStartupGuardHostTests
{
    [Fact]
    public void Startup_Production_WithOidcEnabledAndRequireHttpsMetadataFalse_ThrowsInvalidOperation()
    {
        using var factory = new OidcHttpsMetadataGuardFactory(
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
        using var factory = new OidcHttpsMetadataGuardFactory(
            "Production",
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: "http://google.biatec.io");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("BiatecOidc:RequireHttpsMetadata", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithOidcEnabledAndHttpsAuthority_DoesNotThrow()
    {
        using var factory = new OidcHttpsMetadataGuardFactory(
            "Production",
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: "https://google.biatec.io");

        using var _ = factory.CreateClient();
    }

    [Fact]
    public void Startup_Production_WithOidcDisabled_DoesNotThrow_EvenWithInsecureSettings()
    {
        using var factory = new OidcHttpsMetadataGuardFactory(
            "Production",
            oidcEnabled: false,
            requireHttpsMetadata: false,
            authority: "http://google.biatec.io");

        using var _ = factory.CreateClient();
    }

    [Fact]
    public void Startup_Development_WithOidcEnabledAndRequireHttpsMetadataFalse_DoesNotThrow()
    {
        using var factory = new OidcHttpsMetadataGuardFactory(
            "Development",
            oidcEnabled: true,
            requireHttpsMetadata: false,
            authority: "http://google.biatec.io");

        using var _ = factory.CreateClient();
    }
}

internal sealed class OidcHttpsMetadataGuardFactory(
    string environmentName,
    bool oidcEnabled,
    bool requireHttpsMetadata,
    string authority)
    : WebApplicationFactory<Program>
{
    private const string SafeGameCatalogConnectionString =
        "Host=localhost;Port=5432;Database=game1;Username=postgres;Password=RealSecret123!";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        builder.UseSetting("Jwt:SigningKey", "ProductionStrongSigningKey0123456789ABCDE!");
        builder.UseSetting("ConnectionStrings:GameCatalog", SafeGameCatalogConnectionString);
        builder.UseSetting("Auth:PasswordAuthEnabled", "false");
        builder.UseSetting("Startup:SkipDatabaseInitialization", "true");
        builder.UseSetting("SeedData:AdminEmail", "admin@capitalism.local");
        builder.UseSetting("SeedData:AdminDisplayName", "Platform Admin");
        builder.UseSetting("SeedData:AdminPassword", ApiWebApplicationFactory.TestSeedAdminPassword);
        builder.UseSetting("BiatecOidc:Enabled", oidcEnabled.ToString());
        builder.UseSetting("BiatecOidc:RequireHttpsMetadata", requireHttpsMetadata.ToString());
        builder.UseSetting("BiatecOidc:Authority", authority);
        builder.UseSetting("BiatecOidc:Audience", "capitalism");

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GameCatalog"] = SafeGameCatalogConnectionString,
                ["GameEngine:Enabled"] = "false",
                ["MasterServer:RegistrationEnabled"] = "false",
                ["Auth:PasswordAuthEnabled"] = "false",
                ["Startup:SkipDatabaseInitialization"] = "true",
                ["SeedData:AdminEmail"] = "admin@capitalism.local",
                ["SeedData:AdminDisplayName"] = "Platform Admin",
                ["SeedData:AdminPassword"] = ApiWebApplicationFactory.TestSeedAdminPassword,
                ["BiatecOidc:Enabled"] = oidcEnabled.ToString(),
                ["BiatecOidc:RequireHttpsMetadata"] = requireHttpsMetadata.ToString(),
                ["BiatecOidc:Authority"] = authority,
                ["BiatecOidc:Audience"] = "capitalism",
            });
        });
    }
}
