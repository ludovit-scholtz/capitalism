using Discord;
using Discord.WebSocket;
using MasterApi.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities.Discord;

/// <summary>
/// Hosted service that runs the master-server Discord bot. It registers prefixed slash commands
/// (so a single Discord server can host both staging — <c>cap5stage-*</c> — and production —
/// <c>cap5-*</c> — bots), routes interactions to <see cref="DiscordCommandService"/>, and bridges
/// the configured Discord channel with the in-game chat in both directions.
/// </summary>
public sealed class DiscordBotHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<DiscordBotOptions> _options;
    private readonly DiscordChatRelay _chatRelay;
    private readonly ILogger<DiscordBotHostedService> _logger;
    private DiscordSocketClient? _client;

    public DiscordBotHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<DiscordBotOptions> options,
        DiscordChatRelay chatRelay,
        ILogger<DiscordBotHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _chatRelay = chatRelay;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.IsConfigured())
        {
            _logger.LogInformation("Discord bot disabled or missing token; hosted service will not start.");
            return;
        }

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
            LogLevel = LogSeverity.Info,
        };

        _client = new DiscordSocketClient(config);
        _client.Log += OnLogAsync;
        _client.Ready += OnReadyAsync;
        _client.SlashCommandExecuted += OnSlashCommandAsync;
        _client.MessageReceived += OnMessageReceivedAsync;

        try
        {
            await _client.LoginAsync(TokenType.Bot, options.BotToken);
            await _client.StartAsync();

            await DeliverInGameChatLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord bot terminated unexpectedly.");
        }
        finally
        {
            if (_client is not null)
            {
                await _client.StopAsync();
                await _client.DisposeAsync();
            }
        }
    }

    private async Task DeliverInGameChatLoopAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _chatRelay.ReadAllAsync(stoppingToken))
        {
            var channelId = _options.Value.ChatChannelId;
            if (channelId == 0 || _client is null)
            {
                continue;
            }

            try
            {
                if (await _client.GetChannelAsync(channelId) is IMessageChannel channel)
                {
                    var prefix = string.IsNullOrWhiteSpace(message.ServerName) ? string.Empty : $"[{message.ServerName}] ";
                    await channel.SendMessageAsync($"💬 **{prefix}{message.AuthorDisplayName}**: {message.Content}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver in-game chat message to Discord.");
            }
        }
    }

    private Task OnReadyAsync()
    {
        _ = Task.Run(RegisterCommandsAsync);
        return Task.CompletedTask;
    }

    private async Task RegisterCommandsAsync()
    {
        if (_client is null)
        {
            return;
        }

        var prefix = _options.Value.NormalizedCommandPrefix();

        var verify = new SlashCommandBuilder()
            .WithName($"{prefix}-verify")
            .WithDescription("Link your Discord account to your Capitalism account and claim the Discord bounty.")
            .AddOption("code", ApplicationCommandOptionType.String, "Verification code generated on the master website.", isRequired: true);

        var deposit = new SlashCommandBuilder()
            .WithName($"{prefix}-deposit")
            .WithDescription("Create a tokenized-gold deposit request and get the address and note.")
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("network")
                .WithDescription("Blockchain network (default ALGORAND).")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false)
                .AddChoice("ALGORAND", "ALGORAND")
                .AddChoice("VOI", "VOI"))
            .AddOption("sender", ApplicationCommandOptionType.String, "Optional sending address.", isRequired: false);

        var withdraw = new SlashCommandBuilder()
            .WithName($"{prefix}-withdraw")
            .WithDescription("Create a tokenized-gold withdrawal request.")
            .AddOption("amount", ApplicationCommandOptionType.Number, "Amount of tokenized gold (grams) to withdraw.", isRequired: true)
            .AddOption("address", ApplicationCommandOptionType.String, "Destination blockchain address.", isRequired: true)
            .AddOption(new SlashCommandOptionBuilder()
                .WithName("network")
                .WithDescription("Blockchain network (default ALGORAND).")
                .WithType(ApplicationCommandOptionType.String)
                .WithRequired(false)
                .AddChoice("ALGORAND", "ALGORAND")
                .AddChoice("VOI", "VOI"));

        var help = new SlashCommandBuilder()
            .WithName($"{prefix}-help")
            .WithDescription("Show how to play and the available bot commands.");

        var commands = new ApplicationCommandProperties[]
        {
            verify.Build(),
            deposit.Build(),
            withdraw.Build(),
            help.Build(),
        };

        try
        {
            if (_options.Value.GuildId != 0)
            {
                await _client.Rest.BulkOverwriteGuildCommands(commands, _options.Value.GuildId);
            }
            else
            {
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(commands);
            }

            _logger.LogInformation("Registered {Count} Discord commands with prefix '{Prefix}'.", commands.Length, prefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Discord slash commands.");
        }
    }

    private async Task OnSlashCommandAsync(SocketSlashCommand command)
    {
        var prefix = _options.Value.NormalizedCommandPrefix();
        try
        {
            await command.DeferAsync(ephemeral: true);

            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<DiscordCommandService>();
            var discordUserId = command.User.Id.ToString();
            var username = command.User.Username;

            DiscordCommandResult result;
            switch (TrimPrefix(command.CommandName, prefix))
            {
                case "verify":
                    result = await service.VerifyAsync(discordUserId, username, GetString(command, "code"));
                    break;
                case "deposit":
                    result = await service.CreateDepositAsync(discordUserId, GetString(command, "network"), GetString(command, "sender"));
                    break;
                case "withdraw":
                    result = await service.CreateWithdrawalAsync(
                        discordUserId,
                        GetDecimal(command, "amount"),
                        GetString(command, "address"),
                        GetString(command, "network"));
                    break;
                case "help":
                    result = DiscordCommandResult.Ok(service.BuildHelp());
                    break;
                default:
                    result = DiscordCommandResult.Fail("Unknown command.");
                    break;
            }

            await command.FollowupAsync(result.Message, ephemeral: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling Discord command {CommandName}.", command.CommandName);
            try
            {
                await command.FollowupAsync("An unexpected error occurred. Please try again later.", ephemeral: true);
            }
            catch
            {
                // Interaction already expired; nothing more we can do.
            }
        }
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        var channelId = _options.Value.ChatChannelId;
        if (channelId == 0 || message.Author.IsBot || message.Channel.Id != channelId)
        {
            return;
        }

        var content = message.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.MasterDbContext>();
            var discordUserId = message.Author.Id.ToString();
            var email = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.PlayerAccounts
                    .Where(account => account.DiscordUserId == discordUserId)
                    .Select(account => account.Email));

            if (string.IsNullOrEmpty(email))
            {
                return;
            }

            var bridge = scope.ServiceProvider.GetRequiredService<DiscordGameChatBridge>();
            await bridge.BroadcastAsync(email, content.Length > 500 ? content[..500] : content, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to relay Discord message to in-game chat.");
        }
    }

    private static string TrimPrefix(string commandName, string prefix)
    {
        var marker = prefix + "-";
        return commandName.StartsWith(marker, StringComparison.Ordinal)
            ? commandName[marker.Length..]
            : commandName;
    }

    private static string? GetString(SocketSlashCommand command, string name)
        => command.Data.Options.FirstOrDefault(option => option.Name == name)?.Value?.ToString();

    private static decimal GetDecimal(SocketSlashCommand command, string name)
    {
        var value = command.Data.Options.FirstOrDefault(option => option.Name == name)?.Value;
        return value switch
        {
            double d => (decimal)d,
            long l => l,
            decimal m => m,
            _ => 0m,
        };
    }

    private Task OnLogAsync(LogMessage log)
    {
        _logger.Log(
            log.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                _ => LogLevel.Trace,
            },
            log.Exception,
            "[Discord] {Message}",
            log.Message);
        return Task.CompletedTask;
    }
}
