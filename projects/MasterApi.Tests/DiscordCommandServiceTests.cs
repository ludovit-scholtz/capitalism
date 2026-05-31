using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using MasterApi.Utilities.Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MasterApi.Tests;

public sealed class DiscordCommandServiceTests
{
    private static MasterDbContext NewDb() => new(
        new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase($"discord-{Guid.NewGuid():N}")
            .Options);

    private static DiscordCommandService NewService(
        MasterDbContext db,
        DiscordBotOptions? botOptions = null,
        GoldTokenTransferOptions? transferOptions = null)
    {
        botOptions ??= new DiscordBotOptions { CommandPrefix = "cap5", MasterFrontendUrl = "https://capitalism5.com" };
        transferOptions ??= new GoldTokenTransferOptions
        {
            AlgorandDepositAddress = "ALGO_DEPOSIT_ADDRESS",
            VoiDepositAddress = "VOI_DEPOSIT_ADDRESS",
        };

        return new DiscordCommandService(
            db,
            Options.Create(botOptions),
            Options.Create(transferOptions),
            new MasterRankingService(db, NullLogger<MasterRankingService>.Instance),
            NullLogger<DiscordCommandService>.Instance);
    }

    private static PlayerAccount SeedPlayer(MasterDbContext db, Action<PlayerAccount>? configure = null)
    {
        var player = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = $"player-{Guid.NewGuid():N}@example.com",
            DisplayName = "Player",
            CreatedAtUtc = DateTime.UtcNow,
        };
        configure?.Invoke(player);
        db.PlayerAccounts.Add(player);
        db.SaveChanges();
        return player;
    }

    [Fact]
    public async Task Verify_WithValidCode_LinksAccountAndClearsCode()
    {
        await using var db = NewDb();
        var player = SeedPlayer(db, p =>
        {
            p.DiscordLinkCode = "ABCD2345";
            p.DiscordLinkCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(10);
        });
        var service = NewService(db);

        var result = await service.VerifyAsync("discord-123", "tycoon#1", "abcd2345");

        Assert.True(result.Success);
        var reloaded = await db.PlayerAccounts.FirstAsync(p => p.Id == player.Id);
        Assert.Equal("discord-123", reloaded.DiscordUserId);
        Assert.Null(reloaded.DiscordLinkCode);
        Assert.Null(reloaded.DiscordLinkCodeExpiresAtUtc);
    }

    [Fact]
    public async Task Verify_WithExpiredCode_Fails()
    {
        await using var db = NewDb();
        SeedPlayer(db, p =>
        {
            p.DiscordLinkCode = "EXPIRED1";
            p.DiscordLinkCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        });
        var service = NewService(db);

        var result = await service.VerifyAsync("discord-123", "tycoon", "EXPIRED1");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Verify_WithUnknownCode_Fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.VerifyAsync("discord-123", "tycoon", "NOPECODE");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Deposit_WhenLinked_ReturnsAddressAndNote()
    {
        await using var db = NewDb();
        SeedPlayer(db, p => p.DiscordUserId = "discord-deposit");
        var service = NewService(db);

        var result = await service.CreateDepositAsync("discord-deposit", "ALGORAND", null);

        Assert.True(result.Success);
        Assert.Contains("ALGO_DEPOSIT_ADDRESS", result.Message);
        var request = await db.GoldTokenDepositRequests.FirstAsync();
        Assert.Equal($"CAP-{request.Id}", request.NoteText);
        Assert.Contains(request.NoteText, result.Message);
    }

    [Fact]
    public async Task Deposit_WhenNotLinked_Fails()
    {
        await using var db = NewDb();
        var service = NewService(db);

        var result = await service.CreateDepositAsync("discord-unknown", "ALGORAND", null);

        Assert.False(result.Success);
        Assert.Empty(db.GoldTokenDepositRequests);
    }

    [Fact]
    public async Task Withdraw_WhenLinkedWithBalance_DebitsAndCreatesRequest()
    {
        await using var db = NewDb();
        SeedPlayer(db, p =>
        {
            p.DiscordUserId = "discord-wd";
            p.GoldTokenBalance = 5m;
        });
        var service = NewService(db);

        var result = await service.CreateWithdrawalAsync("discord-wd", 2m, "DEST_ADDRESS", "ALGORAND");

        Assert.True(result.Success);
        var player = await db.PlayerAccounts.FirstAsync(p => p.DiscordUserId == "discord-wd");
        Assert.Equal(3m, player.GoldTokenBalance);
        var request = await db.GoldTokenWithdrawalRequests.FirstAsync();
        Assert.Equal(2m, request.Amount);
        Assert.Equal("DEST_ADDRESS", request.DestinationAddress);
    }

    [Fact]
    public async Task Withdraw_WithInsufficientBalance_FailsWithoutDebit()
    {
        await using var db = NewDb();
        SeedPlayer(db, p =>
        {
            p.DiscordUserId = "discord-poor";
            p.GoldTokenBalance = 1m;
        });
        var service = NewService(db);

        var result = await service.CreateWithdrawalAsync("discord-poor", 2m, "DEST_ADDRESS", "ALGORAND");

        Assert.False(result.Success);
        var player = await db.PlayerAccounts.FirstAsync(p => p.DiscordUserId == "discord-poor");
        Assert.Equal(1m, player.GoldTokenBalance);
        Assert.Empty(db.GoldTokenWithdrawalRequests);
    }

    [Theory]
    [InlineData("cap5", "/cap5-verify")]
    [InlineData("cap5stage", "/cap5stage-verify")]
    public void Help_UsesConfiguredPrefixAndFrontendUrl(string prefix, string expectedCommand)
    {
        using var db = NewDb();
        var service = NewService(db, new DiscordBotOptions
        {
            CommandPrefix = prefix,
            MasterFrontendUrl = "https://capitalism5.com",
            DiscordInviteUrl = "https://discord.gg/PhHSxJvDn6",
        });

        var help = service.BuildHelp();

        Assert.Contains(expectedCommand, help);
        Assert.Contains("https://capitalism5.com", help);
        Assert.Contains("https://discord.gg/PhHSxJvDn6", help);
    }
}
