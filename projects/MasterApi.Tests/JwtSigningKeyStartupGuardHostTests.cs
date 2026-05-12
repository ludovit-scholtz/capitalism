using MasterApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class MasterJwtSigningKeyStartupGuardHostTests
{
    [Fact]
    public void Startup_Testing_WithPlaceholderSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory("Testing", JwtOptions.DefaultSigningKey);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jwt__SigningKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Testing_WithTooShortSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory("Testing", "too-short");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Testing_WithStrongSigningKey_DoesNotThrow()
    {
        using var factory = new MasterJwtSigningKeyGuardFactory("Testing", "MasterHostStrongSigningKey0123456789ABCDEF!");
        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

}

internal sealed class MasterJwtSigningKeyGuardFactory(string environmentName, string? signingKey)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            builder.UseSetting("Jwt:SigningKey", signingKey);
        }

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MasterCatalog"] = $"master-startup-guard-tests-{Guid.NewGuid():N}",
                ["MasterServer:RegistrationKey"] = "test-registration-key",
                ["MasterServer:ActiveThresholdSeconds"] = "90",
                ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
                ["Auth:PasswordAuthEnabled"] = "true",
            });
        });
    }
}
