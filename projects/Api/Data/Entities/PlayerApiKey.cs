using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// A personal API key bound to a player account, enabling programmatic access
/// to the GraphQL API via <c>Authorization: ApiKey &lt;key&gt;</c> header.
/// The plaintext key is shown only once at generation time; only the SHA-256 hash is persisted.
/// </summary>
public sealed class PlayerApiKey
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who owns this key.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Human-readable label assigned by the player.</summary>
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash (hex) of the plaintext key.
    /// The plaintext is never stored; only this hash is used for lookup.
    /// </summary>
    [Required, MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the key was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent request authenticated with this key.</summary>
    public DateTime? LastUsedAtUtc { get; set; }

    /// <summary>Total number of authenticated API calls made with this key.</summary>
    public long TotalCallCount { get; set; }

    /// <summary>When set, the key has been revoked and will be rejected on any future request.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Navigation: the player who owns this key.</summary>
    public Player? Player { get; set; }
}
