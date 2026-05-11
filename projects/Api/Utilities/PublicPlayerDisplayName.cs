using Api.Data.Entities;
using Capitalism.Shared.Security;

namespace Api.Utilities;

public static class PublicPlayerDisplayName
{
    public static string Resolve(Player? player)
        => player is null
            ? string.Empty
            : PlayerDisplayNameProvisioning.ResolveDisplayName(
                player.DisplayName,
                player.Email,
                player.Id.ToString());
}
