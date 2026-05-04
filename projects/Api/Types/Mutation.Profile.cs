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
