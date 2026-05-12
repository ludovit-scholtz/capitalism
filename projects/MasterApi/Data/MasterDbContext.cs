using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Data;

public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public DbSet<GameServerNode> GameServers => Set<GameServerNode>();

    public DbSet<PlayerAccount> PlayerAccounts => Set<PlayerAccount>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<ProSubscription> ProSubscriptions => Set<ProSubscription>();

    public DbSet<GlobalGameAdminGrant> GlobalGameAdminGrants => Set<GlobalGameAdminGrant>();

    public DbSet<GameNewsEntry> GameNewsEntries => Set<GameNewsEntry>();

    public DbSet<GameNewsEntryLocalization> GameNewsEntryLocalizations => Set<GameNewsEntryLocalization>();

    public DbSet<GameNewsReadReceipt> GameNewsReadReceipts => Set<GameNewsReadReceipt>();

    public DbSet<BuildingLayoutTemplate> BuildingLayoutTemplates => Set<BuildingLayoutTemplate>();

    public DbSet<GoldTokenTransaction> GoldTokenTransactions => Set<GoldTokenTransaction>();

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();

    public DbSet<SupportTicketAuditEvent> SupportTicketAuditEvents => Set<SupportTicketAuditEvent>();

    public DbSet<MasterRankingBountyDefinition> MasterRankingBountyDefinitions => Set<MasterRankingBountyDefinition>();

    public DbSet<MasterRankingEvent> MasterRankingEvents => Set<MasterRankingEvent>();

    public DbSet<MasterRankingRewardRecord> MasterRankingRewardRecords => Set<MasterRankingRewardRecord>();

    public DbSet<MasterRankingPlayerSnapshot> MasterRankingPlayerSnapshots => Set<MasterRankingPlayerSnapshot>();

    public DbSet<MasterRankingEvaluationRun> MasterRankingEvaluationRuns => Set<MasterRankingEvaluationRun>();

    public DbSet<MasterRankingBountyAudit> MasterRankingBountyAudits => Set<MasterRankingBountyAudit>();

    public DbSet<RankingTelemetryEventSignature> RankingTelemetryEventSignatures => Set<RankingTelemetryEventSignature>();

    public DbSet<RankingTelemetryAuditLog> RankingTelemetryAuditLogs => Set<RankingTelemetryAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var gameServer = modelBuilder.Entity<GameServerNode>();

        gameServer.HasKey(server => server.Id);
        gameServer.HasIndex(server => server.ServerKey).IsUnique();
        gameServer.HasIndex(server => server.ServerKeyHash).IsUnique();

        gameServer.Property(server => server.ServerKey).HasMaxLength(120);
        gameServer.Property(server => server.ServerKeyHash).HasMaxLength(128);
        gameServer.Property(server => server.DisplayName).HasMaxLength(160);
        gameServer.Property(server => server.Description).HasMaxLength(320);
        gameServer.Property(server => server.Region).HasMaxLength(80);
        gameServer.Property(server => server.Environment).HasMaxLength(80);
        gameServer.Property(server => server.BackendUrl).HasMaxLength(240);
        gameServer.Property(server => server.GraphqlUrl).HasMaxLength(240);
        gameServer.Property(server => server.FrontendUrl).HasMaxLength(240);
        gameServer.Property(server => server.Version).HasMaxLength(80);

        var player = modelBuilder.Entity<PlayerAccount>();
        player.HasKey(p => p.Id);
        player.HasIndex(p => p.Email).IsUnique();
        player.Property(p => p.Email).HasMaxLength(200);
        player.Property(p => p.DisplayName).HasMaxLength(120);
        player.Property(p => p.PasswordHash).HasMaxLength(512);
        player.HasMany(p => p.PasswordResetTokens)
            .WithOne(token => token.PlayerAccount)
            .HasForeignKey(token => token.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        var passwordResetToken = modelBuilder.Entity<PasswordResetToken>();
        passwordResetToken.HasKey(token => token.Id);
        passwordResetToken.HasIndex(token => token.TokenHash).IsUnique();
        passwordResetToken.HasIndex(token => new { token.PlayerAccountId, token.ExpiresAtUtc });
        passwordResetToken.Property(token => token.TokenHash).HasMaxLength(128);

        var sub = modelBuilder.Entity<ProSubscription>();
        sub.HasKey(s => s.Id);
        sub.HasOne(s => s.PlayerAccount)
           .WithMany(p => p.Subscriptions)
           .HasForeignKey(s => s.PlayerAccountId);

        var globalAdminGrant = modelBuilder.Entity<GlobalGameAdminGrant>();
        globalAdminGrant.HasKey(grant => grant.Id);
        globalAdminGrant.HasIndex(grant => grant.Email).IsUnique();
        globalAdminGrant.Property(grant => grant.Email).HasMaxLength(200);
        globalAdminGrant.Property(grant => grant.GrantedByEmail).HasMaxLength(200);

        var newsEntry = modelBuilder.Entity<GameNewsEntry>();
        newsEntry.HasKey(entry => entry.Id);
        newsEntry.HasIndex(entry => new { entry.TargetServerKey, entry.PublishedAtUtc });
        newsEntry.Property(entry => entry.EntryType).HasMaxLength(20);
        newsEntry.Property(entry => entry.Status).HasMaxLength(20);
        newsEntry.Property(entry => entry.TargetServerKey).HasMaxLength(120);
        newsEntry.Property(entry => entry.CreatedByEmail).HasMaxLength(200);
        newsEntry.Property(entry => entry.UpdatedByEmail).HasMaxLength(200);
        newsEntry.HasMany(entry => entry.Localizations)
            .WithOne(localization => localization.GameNewsEntry)
            .HasForeignKey(localization => localization.GameNewsEntryId)
            .OnDelete(DeleteBehavior.Cascade);
        newsEntry.HasMany(entry => entry.ReadReceipts)
            .WithOne(receipt => receipt.GameNewsEntry)
            .HasForeignKey(receipt => receipt.GameNewsEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        var localization = modelBuilder.Entity<GameNewsEntryLocalization>();
        localization.HasKey(entry => entry.Id);
        localization.HasIndex(entry => new { entry.GameNewsEntryId, entry.Locale }).IsUnique();
        localization.Property(entry => entry.Locale).HasMaxLength(10);
        localization.Property(entry => entry.Title).HasMaxLength(220);
        localization.Property(entry => entry.Summary).HasMaxLength(1000);

        var readReceipt = modelBuilder.Entity<GameNewsReadReceipt>();
        readReceipt.HasKey(receipt => receipt.Id);
        readReceipt.HasIndex(receipt => new { receipt.GameNewsEntryId, receipt.PlayerEmail, receipt.ServerKey }).IsUnique();
        readReceipt.Property(receipt => receipt.PlayerEmail).HasMaxLength(200);
        readReceipt.Property(receipt => receipt.ServerKey).HasMaxLength(120);

        var layout = modelBuilder.Entity<BuildingLayoutTemplate>();
        layout.HasKey(l => l.Id);
        layout.HasIndex(l => new { l.PlayerAccountId, l.BuildingType });
        layout.HasOne(l => l.PlayerAccount)
            .WithMany()
            .HasForeignKey(l => l.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        layout.Property(l => l.Name).HasMaxLength(120);
        layout.Property(l => l.Description).HasMaxLength(500);
        layout.Property(l => l.BuildingType).HasMaxLength(60);

        player.Property(p => p.GoldTokenBalance).HasColumnType("decimal(18,8)").HasDefaultValue(0m);
        player.Property(p => p.ConcurrencyToken).IsConcurrencyToken().HasDefaultValueSql("gen_random_uuid()").IsRequired();

        var goldTx = modelBuilder.Entity<GoldTokenTransaction>();
        goldTx.HasKey(tx => tx.Id);
        goldTx.HasIndex(tx => tx.PlayerAccountId);
        goldTx.HasIndex(tx => tx.CreatedAtUtc);
        goldTx.HasOne(tx => tx.PlayerAccount)
            .WithMany(p => p.GoldTokenTransactions)
            .HasForeignKey(tx => tx.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        goldTx.Property(tx => tx.PlayerEmail).HasMaxLength(200);
        goldTx.Property(tx => tx.AdminEmail).HasMaxLength(200);
        goldTx.Property(tx => tx.Note).HasMaxLength(500);
        goldTx.Property(tx => tx.Amount).HasColumnType("decimal(18,8)");
        goldTx.Property(tx => tx.BalanceBefore).HasColumnType("decimal(18,8)");
        goldTx.Property(tx => tx.BalanceAfter).HasColumnType("decimal(18,8)");

        var supportTicket = modelBuilder.Entity<SupportTicket>();
        supportTicket.HasKey(ticket => ticket.Id);
        supportTicket.HasIndex(ticket => new { ticket.CreatedByPlayerAccountId, ticket.CreatedAtUtc });
        supportTicket.HasIndex(ticket => new { ticket.Status, ticket.UpdatedAtUtc });
        supportTicket.HasIndex(ticket => new { ticket.TicketType, ticket.CreatedAtUtc });
        supportTicket.Property(ticket => ticket.CreatedByEmail).HasMaxLength(200);
        supportTicket.Property(ticket => ticket.CreatedByDisplayName).HasMaxLength(120);
        supportTicket.Property(ticket => ticket.TicketType).HasMaxLength(24);
        supportTicket.Property(ticket => ticket.Status).HasMaxLength(24);
        supportTicket.Property(ticket => ticket.Title).HasMaxLength(220);
        supportTicket.Property(ticket => ticket.ModerationState).HasMaxLength(24);
        supportTicket.Property(ticket => ticket.ModerationReason).HasMaxLength(1000);
        supportTicket.Property(ticket => ticket.ModeratedByEmail).HasMaxLength(200);
        supportTicket.Property(ticket => ticket.ExtractedUrlsJson).HasMaxLength(10000);
        supportTicket.Property(ticket => ticket.ExtractedImagesJson).HasMaxLength(10000);
        supportTicket.HasOne(ticket => ticket.CreatedByPlayerAccount)
            .WithMany()
            .HasForeignKey(ticket => ticket.CreatedByPlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        supportTicket.HasMany(ticket => ticket.AuditEvents)
            .WithOne(eventItem => eventItem.SupportTicket)
            .HasForeignKey(eventItem => eventItem.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        var supportAudit = modelBuilder.Entity<SupportTicketAuditEvent>();
        supportAudit.HasKey(eventItem => eventItem.Id);
        supportAudit.HasIndex(eventItem => new { eventItem.SupportTicketId, eventItem.CreatedAtUtc });
        supportAudit.Property(eventItem => eventItem.EventType).HasMaxLength(64);
        supportAudit.Property(eventItem => eventItem.ActorEmail).HasMaxLength(200);
        supportAudit.Property(eventItem => eventItem.ActorDisplayName).HasMaxLength(120);
        supportAudit.Property(eventItem => eventItem.Note).HasMaxLength(1000);
        supportAudit.Property(eventItem => eventItem.MetadataJson).HasMaxLength(10000);

        var rankingDefinition = modelBuilder.Entity<MasterRankingBountyDefinition>();
        rankingDefinition.HasKey(definition => definition.Id);
        rankingDefinition.HasIndex(definition => definition.Code).IsUnique();
        rankingDefinition.Property(definition => definition.Code).HasMaxLength(120);
        rankingDefinition.Property(definition => definition.DisplayName).HasMaxLength(220);
        rankingDefinition.Property(definition => definition.Description).HasMaxLength(1000);
        rankingDefinition.Property(definition => definition.RewardPoints).HasColumnType("decimal(18,4)");
        rankingDefinition.Property(definition => definition.CooldownMode).HasMaxLength(40);
        rankingDefinition.Property(definition => definition.SourceEventType).HasMaxLength(120);
        rankingDefinition.Property(definition => definition.ProofRequirement).HasMaxLength(40);
        rankingDefinition.Property(definition => definition.VisibilityScope).HasMaxLength(40);
        rankingDefinition.Property(definition => definition.ValidationSettingsJson).HasMaxLength(10000);

        var rankingEvent = modelBuilder.Entity<MasterRankingEvent>();
        rankingEvent.HasKey(entry => entry.Id);
        rankingEvent.HasIndex(entry => new { entry.EventType, entry.Status, entry.CreatedAtUtc });
        rankingEvent.HasIndex(entry => entry.ExternalEventId).IsUnique();
        rankingEvent.HasIndex(entry => entry.ProofReference).IsUnique();
        rankingEvent.HasIndex(entry => new { entry.PlayerEmail, entry.EventType, entry.ServerKey, entry.IdempotencyKey });
        rankingEvent.Property(entry => entry.PlayerEmail).HasMaxLength(200);
        rankingEvent.Property(entry => entry.EventType).HasMaxLength(120);
        rankingEvent.Property(entry => entry.ServerKey).HasMaxLength(120);
        rankingEvent.Property(entry => entry.ServerKeyHash).HasMaxLength(128);
        rankingEvent.Property(entry => entry.ExternalEventId).HasMaxLength(220);
        rankingEvent.Property(entry => entry.UniqueScopeKey).HasMaxLength(220);
        rankingEvent.Property(entry => entry.IdempotencyKey).HasMaxLength(220);
        rankingEvent.Property(entry => entry.TelemetryNonce).HasMaxLength(220);
        rankingEvent.Property(entry => entry.PayloadHash).HasMaxLength(128);
        rankingEvent.Property(entry => entry.TelemetrySignatureHash).HasMaxLength(128);
        rankingEvent.Property(entry => entry.Status).HasMaxLength(40);
        rankingEvent.Property(entry => entry.PayloadJson).HasMaxLength(20000);
        rankingEvent.Property(entry => entry.ProofReference).HasMaxLength(1500);
        rankingEvent.Property(entry => entry.ModerationReason).HasMaxLength(1000);
        rankingEvent.Property(entry => entry.ModeratedByEmail).HasMaxLength(200);
        rankingEvent.Property(entry => entry.QuarantineReason).HasMaxLength(1000);
        rankingEvent.Property(entry => entry.QuarantinedByEmail).HasMaxLength(200);
        rankingEvent.Property(entry => entry.QuarantineClearJustification).HasMaxLength(1000);
        rankingEvent.Property(entry => entry.QuarantineClearedByEmail).HasMaxLength(200);
        rankingEvent.HasOne(entry => entry.PlayerAccount)
            .WithMany(playerAccount => playerAccount.RankingEvents)
            .HasForeignKey(entry => entry.PlayerAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        var rankingReward = modelBuilder.Entity<MasterRankingRewardRecord>();
        rankingReward.HasKey(record => record.Id);
        rankingReward.HasIndex(record => record.UniquenessKey).IsUnique();
        rankingReward.HasIndex(record => new { record.PlayerAccountId, record.AwardedAtUtc });
        rankingReward.Property(record => record.PointsAwarded).HasColumnType("decimal(18,4)");
        rankingReward.Property(record => record.Status).HasMaxLength(40);
        rankingReward.Property(record => record.UniquenessKey).HasMaxLength(260);
        rankingReward.Property(record => record.ServerKey).HasMaxLength(120);
        rankingReward.Property(record => record.AwardMetadataJson).HasMaxLength(20000);
        rankingReward.HasOne(record => record.PlayerAccount)
            .WithMany(playerAccount => playerAccount.RankingRewardRecords)
            .HasForeignKey(record => record.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        rankingReward.HasOne(record => record.BountyDefinition)
            .WithMany(definition => definition.RewardRecords)
            .HasForeignKey(record => record.BountyDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
        rankingReward.HasOne(record => record.RankingEvent)
            .WithMany(entry => entry.RewardRecords)
            .HasForeignKey(record => record.RankingEventId)
            .OnDelete(DeleteBehavior.SetNull);

        var rankingSnapshot = modelBuilder.Entity<MasterRankingPlayerSnapshot>();
        rankingSnapshot.HasKey(snapshot => snapshot.Id);
        rankingSnapshot.HasIndex(snapshot => snapshot.PlayerAccountId).IsUnique();
        rankingSnapshot.HasIndex(snapshot => snapshot.GlobalRank);
        rankingSnapshot.Property(snapshot => snapshot.TotalPoints).HasColumnType("decimal(18,4)");
        rankingSnapshot.Property(snapshot => snapshot.LastDailyDecayFactorApplied).HasColumnType("decimal(9,6)");
        rankingSnapshot.HasOne(snapshot => snapshot.PlayerAccount)
            .WithMany()
            .HasForeignKey(snapshot => snapshot.PlayerAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        var rankingRun = modelBuilder.Entity<MasterRankingEvaluationRun>();
        rankingRun.HasKey(run => run.Id);
        rankingRun.HasIndex(run => new { run.RunType, run.StartedAtUtc });
        rankingRun.Property(run => run.RunType).HasMaxLength(40);
        rankingRun.Property(run => run.Status).HasMaxLength(40);
        rankingRun.Property(run => run.TotalPointsAwarded).HasColumnType("decimal(18,4)");
        rankingRun.Property(run => run.TotalPointsBeforeDecay).HasColumnType("decimal(18,4)");
        rankingRun.Property(run => run.TotalPointsAfterDecay).HasColumnType("decimal(18,4)");
        rankingRun.Property(run => run.Notes).HasMaxLength(2000);

        var rankingAudit = modelBuilder.Entity<MasterRankingBountyAudit>();
        rankingAudit.HasKey(audit => audit.Id);
        rankingAudit.HasIndex(audit => new { audit.BountyDefinitionId, audit.CreatedAtUtc });
        rankingAudit.Property(audit => audit.ChangedByEmail).HasMaxLength(200);
        rankingAudit.Property(audit => audit.ChangeType).HasMaxLength(80);
        rankingAudit.Property(audit => audit.PreviousValueJson).HasMaxLength(20000);
        rankingAudit.Property(audit => audit.NewValueJson).HasMaxLength(20000);
        rankingAudit.HasOne(audit => audit.BountyDefinition)
            .WithMany(definition => definition.AuditTrail)
            .HasForeignKey(audit => audit.BountyDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        var telemetrySignature = modelBuilder.Entity<RankingTelemetryEventSignature>();
        telemetrySignature.HasKey(item => item.Id);
        telemetrySignature.HasIndex(item => item.SignatureHash).IsUnique();
        telemetrySignature.Property(item => item.SignatureHash).HasMaxLength(128);
        telemetrySignature.HasIndex(item => item.ExpiresAtUtc);

        var telemetryAudit = modelBuilder.Entity<RankingTelemetryAuditLog>();
        telemetryAudit.HasKey(item => item.Id);
        telemetryAudit.HasIndex(item => new { item.BatchId, item.CreatedAtUtc });
        telemetryAudit.HasIndex(item => item.ReasonCode);
        telemetryAudit.Property(item => item.ServerKeyHash).HasMaxLength(128);
        telemetryAudit.Property(item => item.ServerKeyMasked).HasMaxLength(32);
        telemetryAudit.Property(item => item.EventType).HasMaxLength(120);
        telemetryAudit.Property(item => item.PlayerEmail).HasMaxLength(200);
        telemetryAudit.Property(item => item.EventNonce).HasMaxLength(220);
        telemetryAudit.Property(item => item.PayloadHash).HasMaxLength(128);
        telemetryAudit.Property(item => item.ReasonCode).HasMaxLength(80);
        telemetryAudit.Property(item => item.RawPayloadJson).HasMaxLength(20000);
        telemetryAudit.Property(item => item.QuarantineReason).HasMaxLength(1000);
        telemetryAudit.Property(item => item.QuarantineUpdatedByEmail).HasMaxLength(200);
        telemetryAudit.Property(item => item.ClearJustification).HasMaxLength(1000);
    }
}
