using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Types;

namespace Api.Types;

[ExtendObjectType<Player>]
public sealed class PlayerTypeExtensions
{
    public string GetDisplayName([Parent] Player player)
        => PublicPlayerDisplayName.Resolve(player);

    public string GetPersonalAccountName([Parent] Player player)
        => PublicPlayerDisplayName.Resolve(player);

    public Task<decimal> GetPersonalCash(
        [Parent] Player player,
        [Service] AppDbContext db)
        => PersonalBankAccountService.GetGrossCashAsync(db, player.Id);
}
