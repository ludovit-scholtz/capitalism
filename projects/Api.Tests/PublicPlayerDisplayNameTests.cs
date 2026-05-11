using Api.Data.Entities;
using Api.Utilities;
using Capitalism.Shared.Security;

namespace Api.Tests;

public sealed class PublicPlayerDisplayNameTests
{
    [Fact]
    public void Resolve_NullPlayer_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, PublicPlayerDisplayName.Resolve(null));
    }

    [Fact]
    public void Resolve_ValidDisplayName_ReturnsDisplayName()
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = "captain@example.com",
            DisplayName = "Captain Ledger Fox",
        };

        Assert.Equal("Captain Ledger Fox", PublicPlayerDisplayName.Resolve(player));
    }

    [Fact]
    public void Resolve_SensitiveIdentifier_MatchesProvisionedAlias()
    {
        var player = new Player
        {
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Email = "sensitive@example.com",
            DisplayName = "sensitive@example.com",
        };

        var expected = PlayerDisplayNameProvisioning.ResolveDisplayName(
            player.DisplayName,
            player.Email,
            player.Id.ToString());

        Assert.Equal(expected, PublicPlayerDisplayName.Resolve(player));
        Assert.DoesNotContain("@", PublicPlayerDisplayName.Resolve(player));
    }
}
