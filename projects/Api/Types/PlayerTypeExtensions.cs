using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Types;

namespace Api.Types;

[ExtendObjectType<Player>]
public sealed class PlayerTypeExtensions
{
    public Task<decimal> GetPersonalCash(
        [Parent] Player player,
        [Service] AppDbContext db)
        => PersonalBankAccountService.GetGrossCashAsync(db, player.Id);
}