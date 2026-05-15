using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// A player-authored in-game chat message shown in the shared server-wide chat feed.
/// </summary>
/// <remarks>
/// Chat messages are append-only.  Players identified as
/// <see cref="Player.IsInvisibleInChat"/> are hidden from other players' feeds but
/// their messages are still visible to themselves.
/// </remarks>
public sealed class ChatMessage
{
    /// <summary>Primary key (GUID).</summary>
    public Guid Id { get; set; }

    /// <summary>The player who sent the message.</summary>
    public Guid AuthorPlayerId { get; set; }

    /// <summary>Navigation property to the author.</summary>
    public Player AuthorPlayer { get; set; } = null!;

    /// <summary>
    /// Optional city scope. Null means the global channel.
    /// </summary>
    public Guid? CityId { get; set; }

    /// <summary>
    /// Denormalized author display name for immutable history.
    /// </summary>
    [Required, MaxLength(100)]
    public string AuthorDisplayName { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message.  Maximum 500 characters.
    /// </summary>
    [Required, MaxLength(500)]
    public string Content { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the message was recorded.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft-visibility moderation flag.
    /// </summary>
    public bool IsVisible { get; set; } = true;
}
