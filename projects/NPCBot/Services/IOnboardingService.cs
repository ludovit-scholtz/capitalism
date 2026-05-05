using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Abstraction over the full NPC onboarding flow.
/// Extracted so <see cref="BotOrchestrator"/> can be tested with a fake implementation.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Runs (or resumes) the complete onboarding flow for the given bot.
    /// </summary>
    Task RunAsync(BotAccount bot, string[] allowedIndustries, CancellationToken ct);
}
