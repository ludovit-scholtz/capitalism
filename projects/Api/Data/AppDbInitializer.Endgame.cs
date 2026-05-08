using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private async Task EnsureRealWorldBillionaireBenchmarksAsync()
    {
        var existingByRank = await dbContext.RealWorldBillionaires
            .ToDictionaryAsync(item => item.Rank);
        var nowUtc = DateTime.UtcNow;

        for (var i = 0; i < EndgameCatalog.DefaultTopTenRichestPeople.Count; i++)
        {
            var rank = i + 1;
            var benchmark = EndgameCatalog.DefaultTopTenRichestPeople[i];
            if (existingByRank.TryGetValue(rank, out var existing))
            {
                if (string.IsNullOrWhiteSpace(existing.Name))
                {
                    existing.Name = benchmark.Name;
                }

                if (existing.WealthUsd <= 0m)
                {
                    existing.WealthUsd = benchmark.WealthUsd;
                }
            }
            else
            {
                dbContext.RealWorldBillionaires.Add(new RealWorldBillionaire
                {
                    Id = Guid.NewGuid(),
                    Rank = rank,
                    Name = benchmark.Name,
                    WealthUsd = benchmark.WealthUsd,
                    UpdatedAtUtc = nowUtc,
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
