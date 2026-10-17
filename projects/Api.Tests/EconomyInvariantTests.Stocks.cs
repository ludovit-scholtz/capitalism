using Api.Tests.Infrastructure;

namespace Api.Tests;

public sealed partial class EconomyInvariantTests
{
    /// <summary>
    /// A company buys shares of another company and sells some back through its company account (#607). Money moves to or from
    /// the public market with a stock ledger entry, so per-company conservation must still hold. With
    /// <paramref name="forceBuyThenSell"/> both legs run and must succeed; otherwise one random leg runs.
    /// </summary>
    private static async Task<string?> RandomStockTradeAsync(HttpClient client, World world, Random rng, bool forceBuyThenSell)
    {
        var buyerIdx = rng.Next(world.Players.Count);
        var issuerIdx = (buyerIdx + 1 + rng.Next(world.Players.Count - 1)) % world.Players.Count;
        var buyer = world.Players[buyerIdx];
        var issuer = world.Players[issuerIdx];
        var shares = 1m + rng.Next(0, 5);

        async Task<string?> TradeAsync(string mutation, string input, decimal count) => DescribeError(await TestHelpers.ExecuteGraphQlAsync(
            client,
            $"mutation($input: {input}!) {{ {mutation}(input: $input) {{ __typename }} }}",
            new
            {
                input = new
                {
                    companyId = issuer.CompanyId,
                    shareCount = count,
                    tradeAccountType = "COMPANY",
                    tradeAccountCompanyId = buyer.CompanyId,
                    bankAccountId = buyer.AccountUsd,
                },
            },
            buyer.Token));

        if (forceBuyThenSell || rng.Next(2) == 0)
        {
            var bought = await TradeAsync("buyShares", "BuySharesInput", shares);
            if (bought is not null || !forceBuyThenSell)
            {
                return bought;
            }
        }

        return await TradeAsync("sellShares", "SellSharesInput", forceBuyThenSell ? shares : 1m);
    }
}
