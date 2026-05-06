using System.ComponentModel.DataAnnotations;
using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Updates the authenticated player's profile bio.
    /// The bio is public and visible on the player profile page.
    /// Maximum 160 characters.
    /// </summary>
    [Authorize]
    public async Task<UpdatePlayerBioPayload> UpdatePlayerBio(
        UpdatePlayerBioInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var bio = input.Bio?.Trim();
        if (bio is not null && bio.Length > 160)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bio must be 160 characters or fewer.")
                    .SetCode("BIO_TOO_LONG")
                    .Build());
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (player is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        }

        player.Bio = string.IsNullOrWhiteSpace(bio) ? null : bio;
        await db.SaveChangesAsync(ct);

        return new UpdatePlayerBioPayload
        {
            PlayerId = player.Id,
            Bio = player.Bio,
        };
    }

    /// <summary>
    /// Updates the authenticated player's public display name.
    /// Maximum 100 characters. The display name is shown in rankings,
    /// chat, and on building pages. Players are encouraged not to use
    /// their real name.
    /// </summary>
    [Authorize]
    public async Task<UpdateDisplayNamePayload> UpdateDisplayName(
        UpdateDisplayNameInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var displayName = input.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Display name is required.")
                    .SetCode("DISPLAY_NAME_REQUIRED")
                    .Build());
        }

        if (displayName.Length > 100)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Display name must be 100 characters or fewer.")
                    .SetCode("DISPLAY_NAME_TOO_LONG")
                    .Build());
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (player is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        }

        player.DisplayName = displayName;
        await db.SaveChangesAsync(ct);

        return new UpdateDisplayNamePayload
        {
            PlayerId = player.Id,
            DisplayName = player.DisplayName,
        };
    }
}

/// <summary>Input for the updatePlayerBio mutation.</summary>
public sealed class UpdatePlayerBioInput
{
    /// <summary>
    /// The new bio text (max 160 characters). Pass null or empty string to clear the bio.
    /// </summary>
    [MaxLength(160)]
    public string? Bio { get; set; }
}

/// <summary>Payload returned by the updatePlayerBio mutation.</summary>
public sealed class UpdatePlayerBioPayload
{
    /// <summary>The updated player's identifier.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The updated bio value (null if cleared).</summary>
    public string? Bio { get; set; }
}

/// <summary>Input for the updateDisplayName mutation.</summary>
public sealed class UpdateDisplayNameInput
{
    /// <summary>
    /// The new display name (1–100 characters). Players are encouraged not to use
    /// their real name — use a fictional alias instead.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>Payload returned by the updateDisplayName mutation.</summary>
public sealed class UpdateDisplayNamePayload
{
    /// <summary>The updated player's identifier.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The updated display name.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
