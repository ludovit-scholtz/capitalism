using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot;

/// <summary>
/// Builds the list of <see cref="BotAccount"/> objects from configuration.
/// Extracted as a public static class to allow unit testing.
/// </summary>
public static class BotRosterFactory
{
    private static readonly string[] Strategies =
        ["Trading", "Industrial", "Retail", "Mixed", "Aggressive"];

    /// <summary>
    /// Creates the bot roster. Bot count is clamped to [1, 20].
    /// </summary>
    public static List<BotAccount> Build(BotOptions options)
    {
        var count = Math.Clamp(options.BotCount, 1, 20);
        var roster = new List<BotAccount>(count);

        for (var i = 1; i <= count; i++)
        {
            var strategy = Strategies[(i - 1) % Strategies.Length];
            var name = $"{options.BotNamePrefix}_{strategy}_{i:D2}";
            roster.Add(new BotAccount
            {
                Index = i,
                DisplayName = name,
                Email = $"{name.ToLowerInvariant()}@{options.BotEmailDomain}",
                Strategy = strategy,
            });
        }

        return roster;
    }
}
