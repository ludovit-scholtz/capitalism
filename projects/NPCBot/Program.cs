using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot;

/// <summary>
/// NPC Bot runner entry point.
///
/// Usage:
///   dotnet run                                     # use appsettings.json
///   dotnet run -- --NpcBot:Enabled=false            # disable all bots
///   dotnet run -- --NpcBot:BotCount=5              # run 5 bots
///   dotnet run -- --NpcBot:GraphqlUrl=http://...   # point at a local API
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // ── Build configuration ──────────────────────────────────────────────
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "NPCBOT_")
            .AddCommandLine(args)
            .Build();

        // ── Set up host with DI, logging, and services ───────────────────────
        using var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(b => b.AddConfiguration(config))
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(opts =>
                {
                    opts.TimestampFormat = "HH:mm:ss ";
                    opts.SingleLine = true;
                });
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.Configure<BotOptions>(ctx.Configuration.GetSection(BotOptions.SectionName));

                services.AddHttpClient<GameApiClient>();
                services.AddTransient<AccountService>();
                services.AddTransient<OnboardingService>();
                services.AddTransient<PriceAdjustmentService>();

                // Build the bot roster from config
                services.AddSingleton<IEnumerable<BotAccount>>(sp =>
                {
                    var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;
                    return BotRosterFactory.Build(opts);
                });

                services.AddSingleton<BotOrchestrator>();
            })
            .Build();

        var logger = host.Services.GetRequiredService<ILogger<BotOrchestrator>>();
        var options = host.Services.GetRequiredService<IOptions<BotOptions>>().Value;

        logger.LogInformation("=== Capitalism NPC Bot Runner ===");
        logger.LogInformation("API: {Url}", options.GraphqlUrl);
        logger.LogInformation("Bot count: {Count}", options.BotCount);
        logger.LogInformation("Enabled: {Enabled}", options.Enabled);

        // ── Graceful shutdown ────────────────────────────────────────────────
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            logger.LogInformation("Shutdown requested (Ctrl+C)…");
            cts.Cancel();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => cts.Cancel();

        try
        {
            var orchestrator = host.Services.GetRequiredService<BotOrchestrator>();
            await orchestrator.RunAsync(cts.Token);
            return 0;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Bot runner stopped cleanly.");
            return 0;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Bot runner terminated with an unhandled error.");
            return 1;
        }
    }
}
