using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public static class GovernmentCompanyQueries
{
    public static Task<HashSet<Guid>> GetGovernmentCompanyIdsAsync(AppDbContext db)
        => db.Companies
            .AsNoTracking()
            .Where(company => company.Player.Email == GovernmentActorConstants.GovernmentEmail)
            .Select(company => company.Id)
            .ToHashSetAsync();
}
