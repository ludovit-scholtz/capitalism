using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

/// <summary>
/// <c>ChangeTracker.Entries&lt;T&gt;()</c> first runs a full change-detection pass over every tracked entity (about 0.2 s in a large tick).
/// Phases that only look for entities they themselves added in this tick do not need that (#646).
/// </summary>
public sealed class TrackedEntryReadTests
{
    private static AppDbContext NewDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"tracked-read-{Guid.NewGuid():N}").Options);

    private static Company NewCompany(string name)
        => new() { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = name, FoundedAtUtc = DateTime.UtcNow };

    [Fact]
    public void ReturnsTheEntitiesAddedToTheContext()
    {
        using var db = NewDb();
        var first = NewCompany("First");
        var second = NewCompany("Second");
        db.Companies.AddRange(first, second);

        var entries = db.ChangeTracker.TrackedEntriesWithoutDetection<Company>();

        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, entry => entry.Entity.Id == first.Id);
        Assert.Contains(entries, entry => entry.Entity.Id == second.Id);
        Assert.All(entries, entry => Assert.Equal(EntityState.Added, entry.State));
    }

    [Fact]
    public void ReturnsOnlyEntriesOfTheRequestedType()
    {
        using var db = NewDb();
        db.Companies.Add(NewCompany("Only company"));
        db.Players.Add(new Player { Id = Guid.NewGuid(), Email = "a@b.c", DisplayName = "P", PasswordHash = "x" });

        Assert.Single(db.ChangeTracker.TrackedEntriesWithoutDetection<Company>());
        Assert.Single(db.ChangeTracker.TrackedEntriesWithoutDetection<Player>());
    }

    [Fact]
    public void DoesNotRunChangeDetection_SoAPropertyEditStaysUnnoticed()
    {
        using var db = NewDb();
        var company = NewCompany("Before");
        db.Companies.Add(company);
        db.SaveChanges();
        company.Name = "After"; // a plain property edit: only DetectChanges would notice it

        var entry = Assert.Single(db.ChangeTracker.TrackedEntriesWithoutDetection<Company>());

        Assert.Equal(EntityState.Unchanged, entry.State);
        // The ordinary read does detect it, which is exactly the cost this helper avoids.
        Assert.Equal(EntityState.Modified, Assert.Single(db.ChangeTracker.Entries<Company>()).State);
    }

    [Fact]
    public void RestoresAutomaticChangeDetection()
    {
        using var db = NewDb();
        db.ChangeTracker.AutoDetectChangesEnabled = true;

        _ = db.ChangeTracker.TrackedEntriesWithoutDetection<Company>();

        Assert.True(db.ChangeTracker.AutoDetectChangesEnabled);

        db.ChangeTracker.AutoDetectChangesEnabled = false;
        _ = db.ChangeTracker.TrackedEntriesWithoutDetection<Company>();

        Assert.False(db.ChangeTracker.AutoDetectChangesEnabled); // the caller's setting is kept either way
    }

    [Fact]
    public void SavingStillDetectsPropertyEditsMadeBeforeTheRead()
    {
        using var db = NewDb();
        var company = NewCompany("Before");
        db.Companies.Add(company);
        db.SaveChanges();
        company.Name = "After";

        _ = db.ChangeTracker.TrackedEntriesWithoutDetection<Company>();
        db.SaveChanges();

        Assert.Equal("After", db.Companies.AsNoTracking().Single(c => c.Id == company.Id).Name);
    }
}
