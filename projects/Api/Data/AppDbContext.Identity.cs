using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    private static void ConfigureIdentityEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Email).IsUnique();
            e.Property(p => p.Email).HasMaxLength(256);
            e.Property(p => p.DisplayName).HasMaxLength(100);
            e.Property(p => p.Gender).HasMaxLength(20);
            e.Property(p => p.Role).HasMaxLength(20);
            e.Property(p => p.ActiveAccountType).HasMaxLength(20);
            e.Property(p => p.OnboardingCurrentStep).HasMaxLength(40);
            e.Property(p => p.OnboardingIndustry).HasMaxLength(50);
            e.Property(p => p.ConcurrencyToken).IsConcurrencyToken();
            e.HasMany(p => p.Sessions)
                .WithOne(session => session.Player)
                .HasForeignKey(session => session.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerSession>(e =>
        {
            e.HasKey(session => session.Jti);
            e.Property(session => session.Jti).HasMaxLength(64);
            e.Property(session => session.LastSeenIpAddress).HasMaxLength(64);
            e.Property(session => session.UserAgent).HasMaxLength(512);
            e.Property(session => session.RevokedReason).HasMaxLength(80);
            e.HasIndex(session => new { session.PlayerId, session.LastSeenAtUtc });
            e.HasIndex(session => session.ExpiresAtUtc);
        });

        modelBuilder.Entity<RevokedToken>(e =>
        {
            e.HasKey(token => token.Jti);
            e.Property(token => token.Jti).HasMaxLength(64);
            e.HasIndex(token => token.ExpiresAtUtc);
            e.HasIndex(token => new { token.PlayerId, token.RevokedAtUtc });
        });

        modelBuilder.Entity<PlayerApiKey>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.KeyHash).IsUnique();
            e.HasIndex(k => k.PlayerId);
            e.Property(k => k.Name).HasMaxLength(80);
            e.Property(k => k.KeyHash).HasMaxLength(64);
            e.Property(k => k.Scopes).HasColumnType("text[]");
            e.Property(k => k.CompanyIds).HasColumnType("uuid[]");
            e.HasOne(k => k.Player)
                .WithMany()
                .HasForeignKey(k => k.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerApiKeyAuditLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.OperationName).HasMaxLength(160);
            e.Property(log => log.OperationType).HasMaxLength(16);
            e.Property(log => log.ScopeUsed).HasMaxLength(40);
            e.Property(log => log.DenialCode).HasMaxLength(80);
            e.Property(log => log.DenialReason).HasMaxLength(40);
            e.Property(log => log.AttemptedObjectId).HasMaxLength(64);
            e.Property(log => log.IpAddress).HasMaxLength(64);
            e.Property(log => log.SessionContext).HasMaxLength(128);
            e.HasIndex(log => new { log.PlayerApiKeyId, log.OccurredAtUtc });
            e.HasIndex(log => new { log.PlayerId, log.OccurredAtUtc });
            e.HasOne(log => log.PlayerApiKey)
                .WithMany()
                .HasForeignKey(log => log.PlayerApiKeyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReferralCode>(e =>
        {
            e.HasKey(code => code.Id);
            e.Property(code => code.Code).HasMaxLength(20);
            e.HasIndex(code => code.Code).IsUnique();
            e.HasIndex(code => code.CreatorPlayerId).IsUnique();
            e.HasOne(code => code.CreatorPlayer)
                .WithMany(player => player.CreatedReferralCodes)
                .HasForeignKey(code => code.CreatorPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReferralRegistration>(e =>
        {
            e.HasKey(registration => registration.Id);
            e.HasIndex(registration => registration.ReferredPlayerId).IsUnique();
            e.HasIndex(registration => new { registration.ReferralCodeId, registration.ReferredPlayerId }).IsUnique();
            e.HasOne(registration => registration.ReferralCode)
                .WithMany(code => code.Registrations)
                .HasForeignKey(registration => registration.ReferralCodeId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(registration => registration.ReferredPlayer)
                .WithMany(player => player.ReferralRegistrations)
                .HasForeignKey(registration => registration.ReferredPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.HasKey(message => message.Id);
            e.Property(message => message.Message).HasMaxLength(300);
            e.HasOne(message => message.Player)
                .WithMany()
                .HasForeignKey(message => message.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(message => message.SentAtUtc);
        });

        modelBuilder.Entity<PlayerNotification>(e =>
        {
            e.HasKey(notification => notification.Id);
            e.Property(notification => notification.Type).HasMaxLength(60);
            e.Property(notification => notification.Title).HasMaxLength(160);
            e.Property(notification => notification.Message).HasMaxLength(1000);
            e.Property(notification => notification.Severity).HasMaxLength(16);
            e.Property(notification => notification.TitleKey).HasMaxLength(200);
            e.Property(notification => notification.BodyKey).HasMaxLength(200);
            e.Property(notification => notification.BodyParamsJson).HasMaxLength(4000);
            e.Property(notification => notification.RelatedEntityType).HasMaxLength(60);
            e.HasOne(notification => notification.Player)
                .WithMany(player => player.Notifications)
                .HasForeignKey(notification => notification.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(notification => new { notification.PlayerId, notification.IsRead, notification.CreatedAtTick });
            e.HasIndex(notification => notification.CreatedAtUtc);
            e.HasIndex(notification => new { notification.PlayerId, notification.ExpiresAtUtc });
        });

        modelBuilder.Entity<PersonTradeRecord>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Direction).HasMaxLength(4);
            e.Property(t => t.ShareCount).HasPrecision(18, 4);
            e.Property(t => t.PricePerShare).HasPrecision(18, 4);
            e.Property(t => t.TotalValue).HasPrecision(18, 4);
            e.HasOne(t => t.Player).WithMany().HasForeignKey(t => t.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Company).WithMany().HasForeignKey(t => t.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => t.PlayerId);
            e.HasIndex(t => t.RecordedAtTick);
        });

        modelBuilder.Entity<Company>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.TotalSharesIssued).HasPrecision(18, 4);
            e.Property(c => c.DividendPayoutRatio).HasPrecision(8, 4);
            e.HasOne(c => c.Player).WithMany(p => p.Companies).HasForeignKey(c => c.PlayerId);
            e.HasMany(c => c.BankAccounts)
                .WithOne(account => account.Company)
                .HasForeignKey(account => account.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasMany(c => c.CitySalarySettings)
                .WithOne(setting => setting.Company)
                .HasForeignKey(setting => setting.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.Shareholdings)
                .WithOne(holding => holding.Company)
                .HasForeignKey(holding => holding.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(c => c.DividendPayments)
                .WithOne(payment => payment.Company)
                .HasForeignKey(payment => payment.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shareholding>(e =>
        {
            e.HasKey(holding => holding.Id);
            e.Property(holding => holding.ShareCount).HasPrecision(18, 4);
            e.HasOne(holding => holding.OwnerPlayer)
                .WithMany(player => player.Shareholdings)
                .HasForeignKey(holding => holding.OwnerPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(holding => holding.OwnerCompany)
                .WithMany()
                .HasForeignKey(holding => holding.OwnerCompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(holding => new { holding.CompanyId, holding.OwnerPlayerId });
            e.HasIndex(holding => new { holding.CompanyId, holding.OwnerCompanyId });
        });

        modelBuilder.Entity<DividendPayment>(e =>
        {
            e.HasKey(payment => payment.Id);
            e.Property(payment => payment.ShareCount).HasPrecision(18, 4);
            e.Property(payment => payment.AmountPerShare).HasPrecision(18, 4);
            e.Property(payment => payment.TotalAmount).HasPrecision(18, 4);
            e.Property(payment => payment.Description).HasMaxLength(200);
            e.HasOne(payment => payment.RecipientPlayer)
                .WithMany(player => player.DividendPayments)
                .HasForeignKey(payment => payment.RecipientPlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(payment => payment.RecipientCompany)
                .WithMany()
                .HasForeignKey(payment => payment.RecipientCompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(payment => new { payment.CompanyId, payment.GameYear });
            e.HasIndex(payment => new { payment.RecipientPlayerId, payment.RecordedAtTick });
        });

        modelBuilder.Entity<DividendProposal>(e =>
        {
            e.HasKey(proposal => proposal.Id);
            e.Property(proposal => proposal.StockSymbol).HasMaxLength(40);
            e.Property(proposal => proposal.ProposedByAccountType).HasMaxLength(20);
            e.Property(proposal => proposal.DividendPerShare).HasPrecision(18, 4);
            e.Property(proposal => proposal.TotalPayout).HasPrecision(18, 4);
            e.Property(proposal => proposal.Status).HasMaxLength(20);
            e.HasOne(proposal => proposal.Company)
                .WithMany()
                .HasForeignKey(proposal => proposal.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(proposal => new { proposal.CompanyId, proposal.Status, proposal.VotingCloseTick });
            e.HasIndex(proposal => new { proposal.StockSymbol, proposal.ProposedAtTick });
        });

        modelBuilder.Entity<DividendVote>(e =>
        {
            e.HasKey(vote => vote.Id);
            e.Property(vote => vote.VoterAccountType).HasMaxLength(20);
            e.Property(vote => vote.SharesVoted).HasPrecision(18, 4);
            e.Property(vote => vote.VoteChoice).HasMaxLength(10);
            e.HasOne(vote => vote.Proposal)
                .WithMany(proposal => proposal.Votes)
                .HasForeignKey(vote => vote.ProposalId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(vote => new { vote.ProposalId, vote.VoterAccountId }).IsUnique();
            e.HasIndex(vote => new { vote.ProposalId, vote.VoteChoice });
        });

        modelBuilder.Entity<SharePriceHistoryEntry>(e =>
        {
            e.HasKey(entry => entry.Id);
            e.Property(entry => entry.SharePrice).HasPrecision(18, 4);
            e.HasOne(entry => entry.Company)
                .WithMany()
                .HasForeignKey(entry => entry.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(entry => new { entry.CompanyId, entry.RecordedAtTick, entry.RecordedAtUtc });
        });

        modelBuilder.Entity<CompanyCitySalarySetting>(e =>
        {
            e.HasKey(setting => setting.Id);
            e.HasIndex(setting => new { setting.CompanyId, setting.CityId }).IsUnique();
            e.Property(setting => setting.SalaryMultiplier).HasPrecision(8, 4);
            e.HasOne(setting => setting.City)
                .WithMany()
                .HasForeignKey(setting => setting.CityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TutorialProgress>(e =>
        {
            e.HasKey(tp => tp.Id);
            e.Property(tp => tp.Milestone).HasMaxLength(60);
            e.HasOne(tp => tp.Player)
                .WithMany()
                .HasForeignKey(tp => tp.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(tp => tp.PlayerId);
            e.HasIndex(tp => new { tp.PlayerId, tp.Milestone }).IsUnique();
        });

        modelBuilder.Entity<PlayerAchievementBadge>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BadgeType).HasMaxLength(60);
            e.HasOne(b => b.Player)
                .WithMany()
                .HasForeignKey(b => b.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            // Enforce uniqueness: one badge type per player.
            e.HasIndex(b => new { b.PlayerId, b.BadgeType }).IsUnique();
            e.HasIndex(b => b.PlayerId);
        });

        modelBuilder.Entity<PlayerRankSnapshot>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.WealthUsd).HasPrecision(18, 2);
            e.Property(s => s.PercentileRank).HasPrecision(6, 2);
            e.HasOne(s => s.Player)
                .WithMany()
                .HasForeignKey(s => s.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            // Efficient query by (player, tick) — also enforces uniqueness per tick.
            e.HasIndex(s => new { s.PlayerId, s.SnapshotTick }).IsUnique();
            e.HasIndex(s => s.PlayerId);
        });
    }
}
