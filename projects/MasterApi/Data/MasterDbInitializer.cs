using MasterApi.Data.Entities;
using Capitalism.Shared.Ranking;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Data;

public sealed class MasterDbInitializer(MasterDbContext db, ILogger<MasterDbInitializer> logger)
{
    private static readonly Guid GameAdministrationLaunchEntryId = Guid.Parse("4f31a5d8-4fdf-4d98-9f35-3d8d4f4e5c10");
    private static readonly Guid DirectionalLinksEntryId = Guid.Parse("1a6641d2-4e89-4087-81c5-bbcd0f45ca31");
    private static readonly Guid ManufacturingProductSelectorEntryId = Guid.Parse("a3d807fa-cdd9-4977-ae3a-7e922e9b0755");
    private static readonly Guid NewspaperChangelogRestoredEntryId = Guid.Parse("b8e2f1c3-5a7d-4e9b-8c6f-2d1a3b0e4f5c");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedRankingBountyDefinitionsAsync(cancellationToken);

        await ImportChangelogCsvAsync(cancellationToken);

        // The four entries below are kept as a startup fallback so the feed is never empty
        // if CHANGELOG.csv is unavailable.  If the CSV was already imported (same GUIDs),
        // SeedChangelogEntryAsync is a no-op.
        await SeedChangelogEntryAsync(
            GameAdministrationLaunchEntryId,
            new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
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
            cancellationToken);

        await SeedChangelogEntryAsync(
            DirectionalLinksEntryId,
            new DateTime(2026, 4, 12, 18, 45, 0, DateTimeKind.Utc),
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "en",
                Title = "Building unit directional links with diagonal support",
                Summary = "Unit connections in factories and mines now support all 8 directional link types including diagonals, with visible direction arrows in the grid.",
                HtmlContent = "<p>The building configuration editor now supports full directional unit links.</p><ul><li>Horizontal, vertical, and all four diagonal link directions are available.</li><li>Link direction arrows are shown directly in the 4x4 grid so you can trace the resource flow at a glance.</li><li>The link cycle button steps through none, forward, backward, and diagonal states.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "sk",
                Title = "Smerové prepojenia stavebných jednotiek s uhlopriečnou podporou",
                Summary = "Prepojenia jednotiek v továrňach a baniach teraz podporujú všetkých 8 smerových typov vrátane uhlopriečnych, so viditeľnými šípkami smeru v mriežke.",
                HtmlContent = "<p>Editor konfigurácie budov teraz podporuje plné smerové prepojenia jednotiek.</p><ul><li>Sú k dispozícii horizontálne, vertikálne a všetky štyri uhlopriečne smery prepojenia.</li><li>Šípky smeru prepojenia sú zobrazené priamo v mriežke 4x4, takže tok zdrojov vidíte na prvý pohľad.</li><li>Tlačidlo cyklu prepojenia prechádza stavmi: žiadne, dopredu, dozadu a uhlopriečne.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "de",
                Title = "Direktionale Gebäudeeinheitsverknüpfungen mit Diagonalunterstützung",
                Summary = "Einheitsverbindungen in Fabriken und Minen unterstützen jetzt alle 8 Richtungstypen einschließlich Diagonalen mit sichtbaren Richtungspfeilen im Raster.",
                HtmlContent = "<p>Der Gebäudekonfigurations-Editor unterstützt jetzt vollständige direktionale Einheitsverknüpfungen.</p><ul><li>Horizontale, vertikale und alle vier diagonalen Verknüpfungsrichtungen stehen zur Verfügung.</li><li>Richtungspfeile werden direkt im 4x4-Raster angezeigt, sodass Sie den Ressourcenfluss auf einen Blick verfolgen können.</li><li>Der Zyklus-Knopf wechselt zwischen den Zuständen: keiner, vorwärts, rückwärts und diagonal.</li></ul>",
            },
            cancellationToken);

        await SeedChangelogEntryAsync(
            ManufacturingProductSelectorEntryId,
            new DateTime(2026, 4, 12, 19, 50, 0, DateTimeKind.Utc),
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "en",
                Title = "Manufacturing output product selector shows product images",
                Summary = "The manufacturing unit's output product selector now displays product images alongside names for faster visual identification.",
                HtmlContent = "<p>Configuring your factory's output product is now more intuitive.</p><ul><li>The product selector in manufacturing units now shows the product image next to the name.</li><li>Quickly identify Wooden Chair, Bread, Medicine, and other products at a glance.</li><li>The image tiles make it easier to scan all available options without reading every name.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "sk",
                Title = "Selektor výstupného produktu výroby zobrazuje obrázky produktov",
                Summary = "Selektor výstupného produktu výrobnej jednotky teraz zobrazuje obrázky produktov vedľa názvov pre rýchlejšiu vizuálnu identifikáciu.",
                HtmlContent = "<p>Konfigurácia výstupného produktu vašej továrne je teraz intuitívnejšia.</p><ul><li>Selektor produktov vo výrobných jednotkách teraz zobrazuje obrázok produktu vedľa názvu.</li><li>Rýchlo identifikujte Drevenú stoličku, Chlieb, Lieky a ďalšie produkty na prvý pohľad.</li><li>Obrázkové dlaždice uľahčujú prehľadávanie všetkých dostupných možností bez čítania každého názvu.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "de",
                Title = "Ausgabeprodukt-Selektor der Fertigung zeigt Produktbilder",
                Summary = "Der Ausgabeprodukt-Selektor der Fertigungseinheit zeigt jetzt Produktbilder neben den Namen zur schnelleren visuellen Identifikation.",
                HtmlContent = "<p>Die Konfiguration des Ausgabeprodukts Ihrer Fabrik ist jetzt intuitiver.</p><ul><li>Der Produktselektor in Fertigungseinheiten zeigt jetzt das Produktbild neben dem Namen.</li><li>Identifizieren Sie Holzstuhl, Brot, Medizin und andere Produkte auf einen Blick.</li><li>Die Bildkacheln erleichtern das Durchsuchen aller verfügbaren Optionen ohne jeden Namen lesen zu müssen.</li></ul>",
            },
            cancellationToken);

        await SeedChangelogEntryAsync(
            NewspaperChangelogRestoredEntryId,
            new DateTime(2026, 4, 12, 21, 0, 0, DateTimeKind.Utc),
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "en",
                Title = "Newspaper and changelog data flow restored",
                Summary = "The in-game newspaper and changelog now reliably load from the master API and show player-relevant news and product updates.",
                HtmlContent = "<p>The in-game newspaper is back in full operation.</p><ul><li>All published changelog and news entries are now loaded from the master API and displayed in the News section.</li><li>Unread counts in the navbar update correctly when new entries are published.</li><li>Opening the News page marks all visible entries as read and clears the badge.</li><li>Unauthenticated players can browse the changelog without logging in.</li><li>Error and empty states are shown with clear explanations instead of a blank screen.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "sk",
                Title = "Tok dát novín a changelogu obnovený",
                Summary = "Herné noviny a changelog sa teraz spoľahlivo načítavajú z hlavného API a zobrazujú relevantné novinky a aktualizácie produktov.",
                HtmlContent = "<p>Herné noviny sú opäť v plnej prevádzke.</p><ul><li>Všetky publikované záznamy changelogu a noviniek sa teraz načítavajú z hlavného API a zobrazujú v sekcii Správy.</li><li>Počet neprečítaných správ v navigácii sa správne aktualizuje pri zverejnení nových záznamov.</li><li>Otvorením stránky Správy sa všetky viditeľné záznamy označia ako prečítané a odznak sa vymaže.</li><li>Neautentifikovaní hráči môžu prehliadať changelog bez prihlásenia.</li><li>Chybové a prázdne stavy sa zobrazujú s jasným vysvetlením namiesto prázdnej obrazovky.</li></ul>",
            },
            new GameNewsEntryLocalization
            {
                Id = Guid.NewGuid(),
                Locale = "de",
                Title = "Datenfluss für Zeitung und Changelog wiederhergestellt",
                Summary = "Die In-Game-Zeitung und der Changelog laden jetzt zuverlässig von der Master-API und zeigen spielerrelevante Neuigkeiten und Produktaktualisierungen.",
                HtmlContent = "<p>Die In-Game-Zeitung ist wieder in vollem Betrieb.</p><ul><li>Alle veröffentlichten Changelog- und Neuigkeitseinträge werden jetzt von der Master-API geladen und im Bereich Nachrichten angezeigt.</li><li>Ungelesene Zählungen in der Navigationsleiste werden korrekt aktualisiert, wenn neue Einträge veröffentlicht werden.</li><li>Durch Öffnen der Nachrichtenseite werden alle sichtbaren Einträge als gelesen markiert und das Abzeichen wird gelöscht.</li><li>Nicht authentifizierte Spieler können den Changelog ohne Anmeldung durchsuchen.</li><li>Fehler- und leere Zustände werden mit klaren Erklärungen anstelle eines leeren Bildschirms angezeigt.</li></ul>",
            },
            cancellationToken);
    }

    private async Task SeedChangelogEntryAsync(
        Guid entryId,
        DateTime publishedAt,
        GameNewsEntryLocalization enLocalization,
        GameNewsEntryLocalization skLocalization,
        GameNewsEntryLocalization deLocalization,
        CancellationToken cancellationToken)
    {
        if (await db.GameNewsEntries.AnyAsync(entry => entry.Id == entryId, cancellationToken))
        {
            return;
        }

        db.GameNewsEntries.Add(new GameNewsEntry
        {
            Id = entryId,
            EntryType = GameNewsEntryType.Changelog,
            Status = GameNewsEntryStatus.Published,
            TargetServerKey = null,
            CreatedByEmail = "system@capitalism.local",
            UpdatedByEmail = "system@capitalism.local",
            CreatedAtUtc = publishedAt,
            UpdatedAtUtc = publishedAt,
            PublishedAtUtc = publishedAt,
            Localizations = [enLocalization, skLocalization, deLocalization],
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Reads CHANGELOG.csv from the standard search path and imports any rows whose GUID is not
    /// already in the database.  Called on every startup – fully idempotent.
    /// </summary>
    private async Task ImportChangelogCsvAsync(CancellationToken cancellationToken)
    {
        var csvContent = TryReadChangelogCsv();
        if (csvContent is null)
        {
            logger.LogInformation("CHANGELOG.csv was not found. Skipping changelog import.");
            return;
        }

        var parseResult = ChangelogCsvImporter.ParseCsvWithDiagnostics(csvContent);
        foreach (var parseFailure in parseResult.Failures)
        {
            logger.LogWarning("Skipping malformed CHANGELOG.csv row {LineNumber}: {Reason}", parseFailure.LineNumber, parseFailure.Reason);
        }

        var importer = new ChangelogCsvImporter(db);
        var importResult = await importer.ImportWithDiagnosticsAsync(parseResult.Rows, cancellationToken);

        foreach (var failure in importResult.Failures)
        {
            logger.LogError("Failed to import CHANGELOG.csv entry {EntryId}: {Error}", failure.EntryId, failure.Error);
        }

        logger.LogInformation(
            "CHANGELOG.csv import finished. Imported: {ImportedCount}, duplicates skipped: {DuplicateCount}, failed: {FailedCount}, malformed rows skipped: {MalformedCount}",
            importResult.ImportedCount,
            importResult.DuplicateCount,
            importResult.FailedCount,
            parseResult.Failures.Count);
    }

    /// <summary>
    /// Looks for CHANGELOG.csv in a set of candidate paths so the import works in both
    /// production (build output) and local development (repo root).
    /// </summary>
    internal static string? TryReadChangelogCsv()
    {
        var baseDir = AppContext.BaseDirectory;

        var candidates = new[]
        {
            // Production / Docker: file is copied next to the binary during CI build.
            System.IO.Path.Combine(baseDir, "CHANGELOG.csv"),
            // Local development: repo root is a few levels above the bin directory.
            System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "..", "CHANGELOG.csv")),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "CHANGELOG.csv")),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "..", "CHANGELOG.csv")),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "CHANGELOG.csv")),
            // Current working directory fallback.
            "CHANGELOG.csv",
        };

        foreach (var path in candidates)
        {
            try
            {
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            catch
            {
                // Ignore access errors and try the next candidate.
            }
        }

        return null;
    }

    private async Task SeedRankingBountyDefinitionsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var defaults = new List<MasterRankingBountyDefinition>
        {
            BuildDefaultDefinition(MasterRankingBountyCodes.GameImprover, "Game improver", "Submit a suggestion or bug report through support.", 5m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.RecommendFriend, "Recommend a friend", "A referred player creates a valid account.", 5m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.RecommendGoodFriend, "Recommend a good friend", "A referred player purchases startup pack or Pro.", 100m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.RetweetXPost, "Retweet a X post", "Submit a social post proof and pass moderation.", 5m, RankingCooldownMode.PerUniqueKey, RankingProofRequirement.Url, RankingVisibilityScope.AdminOnly, requiresModeration: true),
            BuildDefaultDefinition(MasterRankingBountyCodes.DiscordPlayer, "Discord player", "Complete Discord ownership verification.", 50m, RankingCooldownMode.Once, RankingProofRequirement.DiscordHandle, RankingVisibilityScope.AdminOnly, requiresModeration: true),
            BuildDefaultDefinition(MasterRankingBountyCodes.LoginToGame, "Log in to game", "Open dashboard on a distinct game server.", 5m, RankingCooldownMode.UtcDayPerServer, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.Manufacturer, "Manufacturer", "Factory produces any product quantity.", 1m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.Wholesaler, "Wholesaler", "Sales shop sells any product quantity.", 1m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.Researcher, "Researcher", "Any R&D unit has active budget.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.RealEstateMagnate, "Real estate magnate", "Owned apartment or commercial buildings have occupancy.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.MediaOwner, "Media owner", "Owned media houses have active content budget.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.Banker, "Banker", "Another user deposits funds into your bank.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.Lender, "Lender", "Another user maintains an active loan in your bank.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.FxTrader, "FX trader", "Complete any currency swap.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.StockTrader, "Stock trader", "Buy any stock.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.EnergyTrader, "Energy trader", "Power plant ships energy to market.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.GoodEmployer, "Good employer", "Highest wage rate in an actively paid city.", 10m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.DividendsMaster, "Dividends master", "Owned company pays dividends to shareholders.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.TopPlayer, "Top player", "Player rank is in the top ten.", 5m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.GreatPlayer, "Great player", "Player rank is in the top hundred.", 2m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
            BuildDefaultDefinition(MasterRankingBountyCodes.CompanyMaster, "Company master", "Any owned company rank is in top ten.", 5m, RankingCooldownMode.UtcDay, RankingProofRequirement.None, RankingVisibilityScope.PlayerHistory),
        };

        defaults.AddRange(TutorialRankingBountyCatalog.All.Select(item =>
            BuildDefaultDefinition(
                item.BountyCode,
                item.DisplayName,
                item.Description,
                item.RewardPoints,
                RankingCooldownMode.Once,
                RankingProofRequirement.None,
                RankingVisibilityScope.PlayerHistory)));

        foreach (var definition in defaults)
        {
            var existing = await db.MasterRankingBountyDefinitions
                .FirstOrDefaultAsync(item => item.Code == definition.Code, cancellationToken);

            if (existing is null)
            {
                definition.Id = Guid.NewGuid();
                definition.CreatedAtUtc = now;
                definition.UpdatedAtUtc = now;
                db.MasterRankingBountyDefinitions.Add(definition);
                continue;
            }

            existing.DisplayName = definition.DisplayName;
            existing.Description = definition.Description;
            existing.RewardPoints = definition.RewardPoints;
            existing.CooldownMode = definition.CooldownMode;
            existing.ProofRequirement = definition.ProofRequirement;
            existing.VisibilityScope = definition.VisibilityScope;
            existing.RequiresModeration = definition.RequiresModeration;
            existing.SourceEventType = definition.SourceEventType;
            existing.ValidationSettingsJson = definition.ValidationSettingsJson;
            existing.IsVisibleToPlayers = definition.IsVisibleToPlayers;
            existing.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static MasterRankingBountyDefinition BuildDefaultDefinition(
        string code,
        string displayName,
        string description,
        decimal rewardPoints,
        string cooldownMode,
        string proofRequirement,
        string visibilityScope,
        bool requiresModeration = false)
    {
        return new MasterRankingBountyDefinition
        {
            Code = code,
            DisplayName = displayName,
            Description = description,
            RewardPoints = rewardPoints,
            IsEnabled = true,
            IsVisibleToPlayers = true,
            RequiresModeration = requiresModeration,
            CooldownMode = cooldownMode,
            SourceEventType = code,
            ProofRequirement = proofRequirement,
            VisibilityScope = visibilityScope,
            ValidationSettingsJson = "{}",
        };
    }

}
