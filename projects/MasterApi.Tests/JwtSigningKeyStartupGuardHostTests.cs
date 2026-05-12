using MasterApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class MasterJwtSigningKeyStartupGuardHostTests
{
    [Fact]
    public void Startup_Production_WithPlaceholderSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory("Production", JwtOptions.DefaultSigningKey);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jwt__SigningKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithTooShortSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory("Production", "too-short");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderMasterConnectionString_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            masterCatalogConnectionString: "__SET_IN_ENV__");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:MasterCatalog", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConnectionStrings__MasterCatalog", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderRootAdministratorEmail_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            masterCatalogConnectionString: "Host=localhost;Port=5432;Database=master-guard;Username=postgres;Password=not-used-in-guard",
            rootAdministratorEmail: "__SET_IN_ENV__");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("GameAdministration:RootAdministratorEmails", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GameAdministration__RootAdministratorEmails__0", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Testing_WithPlaceholderRootAdministratorEmail_DoesNotThrow()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory(
            "Testing",
            JwtOptions.DefaultSigningKey,
            rootAdministratorEmail: "__SET_IN_ENV__",
            masterCatalogConnectionString: "__SET_IN_ENV__");

        using var _ = factory.CreateClient();
    }

}

internal sealed class MasterJwtSigningKeyGuardFactory(
    string environmentName,
    string? signingKey,
    string? masterCatalogConnectionString = null,
    string? rootAdministratorEmail = "root@example.com")
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            builder.UseSetting("Jwt:SigningKey", signingKey);
        }
        if (!string.IsNullOrWhiteSpace(masterCatalogConnectionString))
        {
            builder.UseSetting("ConnectionStrings:MasterCatalog", masterCatalogConnectionString);
        }
        if (!string.IsNullOrWhiteSpace(rootAdministratorEmail))
        {
            builder.UseSetting("GameAdministration:RootAdministratorEmails:0", rootAdministratorEmail);
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterCatalog"] = masterCatalogConnectionString ?? $"master-startup-guard-tests-{Guid.NewGuid():N}",
                ["MasterServer:RegistrationKey"] = "test-registration-key",
                ["MasterServer:ActiveThresholdSeconds"] = "90",
                ["GameAdministration:RootAdministratorEmails:0"] = rootAdministratorEmail,
                ["Auth:PasswordAuthEnabled"] = "true",
            });
        });
    }
}
