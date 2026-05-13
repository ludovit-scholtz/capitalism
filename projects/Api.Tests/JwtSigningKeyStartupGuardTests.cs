using Api.Security;
using Api.Tests.Infrastructure;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

public sealed class JwtSigningKeyStartupGuardTests
{
    [Fact]
    public void StartupGuard_ThrowsInvalidOperationException_WhenKeyIsPlaceholder_InProduction()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GuardAndThrowForNonDevelopment(JwtOptions.DefaultSigningKey, isDevelopment: false));

        Assert.Contains("placeholder", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartupGuard_ThrowsInvalidOperationException_WhenKeyIsTooShort_InProduction()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            GuardAndThrowForNonDevelopment("short-key", isDevelopment: false));

        Assert.Contains("shorter than", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartupGuard_ThrowsInvalidOperationException_WhenKeyIsNullOrEmpty_InProduction()
    {
        Assert.Throws<InvalidOperationException>(() => GuardAndThrowForNonDevelopment(null, isDevelopment: false));
        Assert.Throws<InvalidOperationException>(() => GuardAndThrowForNonDevelopment(string.Empty, isDevelopment: false));
        Assert.Throws<InvalidOperationException>(() => GuardAndThrowForNonDevelopment("   ", isDevelopment: false));
    }

    [Fact]
    public void StartupGuard_DoesNotThrow_WhenKeyIsStrong_InProduction()
    {
        GuardAndThrowForNonDevelopment("ProductionStrongSigningKey0123456789ABCDE!", isDevelopment: false);
    }

    [Fact]
    public void StartupGuard_DoesNotThrow_WhenKeyIsPlaceholder_InDevelopment()
    {
        GuardAndThrowForNonDevelopment(JwtOptions.DefaultSigningKey, isDevelopment: true);
    }

    [Fact]
    public void StartupGuard_DoesNotThrow_WhenKeyIsStrong_InDevelopment()
    {
        GuardAndThrowForNonDevelopment("DevelopmentStrongSigningKey0123456789ABCD!", isDevelopment: true);
    }

    private static void GuardAndThrowForNonDevelopment(string? signingKey, bool isDevelopment)
    {
        if (!isDevelopment
            && JwtSigningKeyStartupGuard.TryGetUnsafeReason(signingKey, [JwtOptions.DefaultSigningKey], out var reason))
        {
            throw new InvalidOperationException(reason);
        }
    }
}

public sealed class ApiJwtSigningKeyStartupGuardHostTests
{
    [Fact]
    public void Startup_Production_WithPlaceholderSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory("Production", JwtOptions.DefaultSigningKey);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Jwt__SigningKey", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithTooShortSigningKey_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory("Production", "too-short");
        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("Jwt:SigningKey is set to a placeholder or insecure value", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderGameCatalogConnectionString_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            gameCatalogConnectionString: "__SET_IN_ENV__");

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("ConnectionStrings:GameCatalog", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConnectionStrings__GameCatalog", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderSeedAdminPasswordAndPasswordAuthEnabled_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            seedAdminPassword: "changeme",
            passwordAuthEnabled: true);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("SeedData:AdminPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SeedData__AdminPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderSeedAdminPasswordAndPasswordAuthSettingOmitted_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            seedAdminPassword: "changeme",
            passwordAuthEnabled: null);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("SeedData:AdminPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Production_WithPlaceholderSeedAdminPasswordAndPasswordAuthDisabled_DoesNotThrow()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            seedAdminPassword: "changeme",
            passwordAuthEnabled: false);

        using var _ = factory.CreateClient();
    }

    [Fact]
    public void Startup_Production_WithMissingSeedDataSectionAndPasswordAuthEnabled_ThrowsInvalidOperation()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Production",
            "ProductionStrongSigningKey0123456789ABCDE!",
            includeSeedDataSection: false,
            passwordAuthEnabled: true);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("SeedData:AdminPassword", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Startup_Development_WithPlaceholderSeedAdminPasswordAndPasswordAuthEnabled_DoesNotThrow()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Development",
            JwtOptions.DefaultSigningKey,
            seedAdminPassword: "changeme",
            passwordAuthEnabled: true);

        using var _ = factory.CreateClient();
    }

    [Fact]
    public void Startup_Testing_WithPlaceholderGameCatalogConnectionString_DoesNotThrow()
    {
        using var factory = new ApiJwtSigningKeyGuardFactory(
            "Testing",
            JwtOptions.DefaultSigningKey,
            gameCatalogConnectionString: "__SET_IN_ENV__");

        using var _ = factory.CreateClient();
    }
}

internal sealed class ApiJwtSigningKeyGuardFactory(
    string environmentName,
    string? signingKey,
    string? gameCatalogConnectionString = null,
    string? seedAdminPassword = null,
    bool? passwordAuthEnabled = null,
    bool includeSeedDataSection = true)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environmentName);
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            builder.UseSetting("Jwt:SigningKey", signingKey);
        }

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:GameCatalog"] = gameCatalogConnectionString ?? $"startup-guard-tests-{Guid.NewGuid():N}",
                ["GameEngine:Enabled"] = "false",
                ["MasterServer:RegistrationEnabled"] = "false",
                ["Auth:PasswordAuthEnabled"] = (passwordAuthEnabled ?? true).ToString(),
            };

            if (includeSeedDataSection)
            {
                values["SeedData:AdminEmail"] = "admin@capitalism.local";
                values["SeedData:AdminDisplayName"] = "Platform Admin";
                values["SeedData:AdminPassword"] = seedAdminPassword ?? ApiWebApplicationFactory.TestSeedAdminPassword;
            }

            configurationBuilder.AddInMemoryCollection(values);
        });
    }
}
