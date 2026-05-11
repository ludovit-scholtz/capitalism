using Capitalism.NPCBot.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="BotStartupValidator"/>.
/// Covers the startup credential guard that prevents the NPC bot from running
/// outside the Development environment with a placeholder password.
/// </summary>
public sealed class BotStartupValidatorTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private sealed class FakeHostEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "NPCBot";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static readonly IHostEnvironment Production = new FakeHostEnv("Production");
    private static readonly IHostEnvironment Staging = new FakeHostEnv("Staging");
    private static readonly IHostEnvironment Development = new FakeHostEnv("Development");

    // ── Placeholder detection: IsPlaceholder ─────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("NpcBot!2025")]
    [InlineData("changeme")]
    [InlineData("CHANGEME")]
    [InlineData("default")]
    [InlineData("DEFAULT")]
    [InlineData("password")]
    [InlineData("secret")]
    public void IsPlaceholder_ReturnsTrue_ForKnownWeakValues(string value)
    {
        Assert.True(BotStartupValidator.IsPlaceholder(value),
            $"Expected '{value}' to be recognised as a placeholder.");
    }

    [Theory]
    [InlineData("a1b2c3d4e5f6g7h8")]
    [InlineData("MyStr0ng!Pass#2025")]
    [InlineData("correct-horse-battery-staple")]
    [InlineData("openssl-rand-hex-output-here")]
    public void IsPlaceholder_ReturnsFalse_ForStrongValues(string value)
    {
        Assert.False(BotStartupValidator.IsPlaceholder(value),
            $"Expected '{value}' NOT to be recognised as a placeholder.");
    }

    // ── Validate: API-key mode bypasses password check ────────────────────────

    [Fact]
    public void Validate_ApiKeyMode_DoesNotThrow_EvenWithPlaceholderPassword()
    {
        var options = new BotOptions
        {
            Enabled = true,
            ApiKey = "real-api-key-value",
            BotPassword = "NpcBot!2025", // placeholder — should be ignored in API-key mode
        };

        // Should not throw: API-key mode skips password validation.
        BotStartupValidator.Validate(options, Production);
    }

    // ── Validate: Development environment bypasses check ─────────────────────

    [Fact]
    public void Validate_DevelopmentEnvironment_DoesNotThrow_WithPlaceholderPassword()
    {
        var options = new BotOptions
        {
            Enabled = true,
            BotPassword = "NpcBot!2025",
        };

        // Should not throw: Development env allows placeholder for local convenience.
        BotStartupValidator.Validate(options, Development);
    }

    [Fact]
    public void Validate_DevelopmentEnvironment_DoesNotThrow_WithEmptyPassword()
    {
        var options = new BotOptions { Enabled = true, BotPassword = "" };
        BotStartupValidator.Validate(options, Development);
    }

    // ── Validate: Disabled bot does not fail even without a password ──────────

    [Fact]
    public void Validate_DisabledBot_DoesNotThrow_WithPlaceholderPassword()
    {
        var options = new BotOptions { Enabled = false, BotPassword = "changeme" };

        // Should not throw: disabled bots never authenticate, so a missing
        // password is harmless.
        BotStartupValidator.Validate(options, Production);
    }

    // ── Validate: Production with placeholder throws ──────────────────────────

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Validate_NonDevEnv_ThrowsInvalidOperationException_WhenPasswordIsPlaceholder(string envName)
    {
        var options = new BotOptions { Enabled = true, BotPassword = "NpcBot!2025" };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BotStartupValidator.Validate(options, new FakeHostEnv(envName)));

        Assert.Contains("NpcBot__BotPassword", ex.Message);
        Assert.Contains("NpcBot__ApiKey", ex.Message);
    }

    [Fact]
    public void Validate_ProductionWithEmptyPassword_ThrowsInvalidOperationException()
    {
        var options = new BotOptions { Enabled = true, BotPassword = "" };

        Assert.Throws<InvalidOperationException>(() =>
            BotStartupValidator.Validate(options, Production));
    }

    [Theory]
    [InlineData("changeme")]
    [InlineData("default")]
    [InlineData("password")]
    [InlineData("secret")]
    public void Validate_Production_ThrowsForAllKnownPlaceholders(string placeholder)
    {
        var options = new BotOptions { Enabled = true, BotPassword = placeholder };

        Assert.Throws<InvalidOperationException>(() =>
            BotStartupValidator.Validate(options, Production));
    }

    // ── Validate: Production with real secret passes ──────────────────────────

    [Fact]
    public void Validate_ProductionWithStrongPassword_DoesNotThrow()
    {
        var options = new BotOptions
        {
            Enabled = true,
            BotPassword = "correct-horse-battery-staple-2025!",
        };

        // Should not throw: the password is a real (non-placeholder) value.
        BotStartupValidator.Validate(options, Production);
    }

    [Fact]
    public void Validate_StagingWithStrongPassword_DoesNotThrow()
    {
        var options = new BotOptions
        {
            Enabled = true,
            BotPassword = "Str0ng&Unique!Staging#Pass",
        };

        BotStartupValidator.Validate(options, Staging);
    }

    // ── KnownPlaceholders constant list ───────────────────────────────────────

    [Fact]
    public void KnownPlaceholders_ContainsEmptyString()
    {
        // The empty string is the new default; it must always be rejected outside dev.
        Assert.Contains("", BotStartupValidator.KnownPlaceholders, StringComparer.Ordinal);
    }

    [Fact]
    public void KnownPlaceholders_ContainsRemovedLegacyDefault()
    {
        // "NpcBot!2025" was the committed placeholder that this feature removes.
        // Keeping it in the list ensures old deployments that still have the value
        // set explicitly are also blocked.
        Assert.Contains("NpcBot!2025", BotStartupValidator.KnownPlaceholders, StringComparer.Ordinal);
    }
}
