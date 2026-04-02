namespace MasterApi.Data;

public sealed class MasterDbInitializer(MasterDbContext db)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
    }
}