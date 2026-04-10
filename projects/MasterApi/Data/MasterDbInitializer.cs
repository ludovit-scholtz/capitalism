using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Data;

public sealed class MasterDbInitializer(MasterDbContext db)
{
    private static readonly Guid GameAdministrationLaunchEntryId = Guid.Parse("4f31a5d8-4fdf-4d98-9f35-3d8d4f4e5c10");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.GameNewsEntries.AnyAsync(entry => entry.Id == GameAdministrationLaunchEntryId, cancellationToken))
        {
            db.GameNewsEntries.Add(new GameNewsEntry
            {
                Id = GameAdministrationLaunchEntryId,
                EntryType = GameNewsEntryType.Changelog,
                Status = GameNewsEntryStatus.Published,
                TargetServerKey = null,
                CreatedByEmail = "system@capitalism.local",
                UpdatedByEmail = "system@capitalism.local",
                CreatedAtUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAtUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
                PublishedAtUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
                Localizations =
                [
                    new GameNewsEntryLocalization
                    {
                        Id = Guid.NewGuid(),
                        Locale = "en",
                        Title = "Game administration and newsroom launched",
                        Summary = "The game now ships a shared newspaper, changelog feed, unread tracking, and a live admin operations dashboard.",
                        HtmlContent = "<p>The shared newsroom is now live in every game shard.</p><ul><li>Published news and changelog posts now come from MasterApi.</li><li>Players see unread counts directly in the navbar.</li><li>Administrators can review anomalies, manage roles, toggle invisible support mode, and impersonate accounts with audit logs.</li></ul>",
                    },
                    new GameNewsEntryLocalization
                    {
                        Id = Guid.NewGuid(),
                        Locale = "sk",
                        Title = "Správa hry a newsroom sú spustené",
                        Summary = "Hra teraz obsahuje zdieľané noviny, changelog feed, sledovanie neprečítaných správ a živý administračný dashboard.",
                        HtmlContent = "<p>Zdieľaný newsroom je teraz aktívny na každom hernom serveri.</p><ul><li>Publikované novinky a changelog sa načítavajú z MasterApi.</li><li>Hráči vidia počet neprečítaných správ priamo v navigácii.</li><li>Administrátori môžu sledovať anomálie, spravovať roly, prepínať neviditeľný režim podpory a impersonovať účty s audit logom.</li></ul>",
                    },
                    new GameNewsEntryLocalization
                    {
                        Id = Guid.NewGuid(),
                        Locale = "de",
                        Title = "Spieladministration und Newsroom gestartet",
                        Summary = "Das Spiel enthält jetzt eine gemeinsame Zeitung, einen Changelog-Feed, ungelesene Hinweise in der Navigation und ein Live-Admin-Dashboard.",
                        HtmlContent = "<p>Der gemeinsame Newsroom ist jetzt auf jedem Spielserver aktiv.</p><ul><li>Veröffentlichte News und Changelog-Einträge kommen jetzt aus der MasterApi.</li><li>Spieler sehen die Anzahl ungelesener Meldungen direkt in der Navigation.</li><li>Administratoren können Auffälligkeiten prüfen, Rollen verwalten, den unsichtbaren Support-Modus schalten und Konten mit Audit-Log impersonieren.</li></ul>",
                    },
                ],
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}