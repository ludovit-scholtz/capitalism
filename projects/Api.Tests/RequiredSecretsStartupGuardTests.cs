using Capitalism.Shared.Security;

namespace Api.Tests;

public sealed class RequiredSecretsStartupGuardTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("__SET_IN_ENV__")]
    [InlineData("Host=localhost;Password=CHANGE_ME")]
    [InlineData("Host=localhost;Password=<REQUIRED_SECRET>")]
    public void TryGetUnsafeConnectionStringReason_ReturnsTrue_ForMissingOrPlaceholderValues(string? connectionString)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeConnectionStringReason(connectionString, out var reason);

        Assert.True(isUnsafe);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=game1;Username=postgres;Password=RealSecret123!")]
    [InlineData("Host=db;Database=prod;Username=app;Password=StrongPassword")]
    public void TryGetUnsafeConnectionStringReason_ReturnsFalse_ForValidValues(string connectionString)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeConnectionStringReason(connectionString, out var reason);

        Assert.False(isUnsafe);
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void TryGetUnsafeRootAdministratorEmailsReason_ReturnsTrue_ForMissingOrWhitespaceOnly()
    {
        Assert.True(RequiredSecretsStartupGuard.TryGetUnsafeRootAdministratorEmailsReason(null, out var nullReason));
        Assert.Contains("empty", nullReason, StringComparison.OrdinalIgnoreCase);

        Assert.True(RequiredSecretsStartupGuard.TryGetUnsafeRootAdministratorEmailsReason(["   "], out var whitespaceReason));
        Assert.Contains("empty", whitespaceReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("__SET_IN_ENV__")]
    [InlineData("CHANGE_ME@example.com")]
    [InlineData("<REQUIRED>@example.com")]
    public void TryGetUnsafeRootAdministratorEmailsReason_ReturnsTrue_ForPlaceholderValues(string rootEmail)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeRootAdministratorEmailsReason([rootEmail], out var reason);

        Assert.True(isUnsafe);
        Assert.Contains("placeholder", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetUnsafeRootAdministratorEmailsReason_ReturnsFalse_ForValidValues()
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeRootAdministratorEmailsReason(
            [" root-admin@example.com ", "security@example.com"],
            out var reason);

        Assert.False(isUnsafe);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("__SET_IN_ENV__")]
    [InlineData("changeme")]
    [InlineData("CHANGEME")]
    [InlineData("password")]
    [InlineData("PassWord")]
    [InlineData("admin")]
    [InlineData("seed")]
    [InlineData("default")]
    public void TryGetUnsafeSeedAdminPasswordReason_ReturnsTrue_ForMissingOrPlaceholderValues(string? adminPassword)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeSeedAdminPasswordReason(adminPassword, out var reason);

        Assert.True(isUnsafe);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData("StrongSeedPassword123!")]
    [InlineData("prod-seed-admin-password-2026")]
    public void TryGetUnsafeSeedAdminPasswordReason_ReturnsFalse_ForStrongValue(string adminPassword)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeSeedAdminPasswordReason(adminPassword, out var reason);

        Assert.False(isUnsafe);
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void TryGetUnsafeOidcHttpsMetadataReason_ReturnsFalse_WhenOidcDisabled()
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason(
            oidcEnabled: false,
            requireHttpsMetadata: false,
            authority: "http://insecure.example.com",
            out var reason);

        Assert.False(isUnsafe);
        Assert.Equal(string.Empty, reason);
    }

    [Fact]
    public void TryGetUnsafeOidcHttpsMetadataReason_ReturnsTrue_WhenRequireHttpsMetadataDisabled()
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason(
            oidcEnabled: true,
            requireHttpsMetadata: false,
            authority: "https://google.biatec.io",
            out var reason);

        Assert.True(isUnsafe);
        Assert.Contains("RequireHttpsMetadata", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("http://google.biatec.io")]
    public void TryGetUnsafeOidcHttpsMetadataReason_ReturnsTrue_ForMissingOrNonHttpsAuthority(string? authority)
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason(
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: authority,
            out var reason);

        Assert.True(isUnsafe);
        Assert.Contains("HTTPS", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryGetUnsafeOidcHttpsMetadataReason_ReturnsFalse_ForHttpsAuthorityAndRequireHttpsMetadataEnabled()
    {
        var isUnsafe = RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason(
            oidcEnabled: true,
            requireHttpsMetadata: true,
            authority: "https://google.biatec.io",
            out var reason);

        Assert.False(isUnsafe);
        Assert.Equal(string.Empty, reason);
    }
}
