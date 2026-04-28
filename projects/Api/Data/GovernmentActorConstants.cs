namespace Api.Data;

/// <summary>
/// Well-known identity constants for the government system actor.
/// The government is a special internal player that participates in economic simulations
/// (taxes, loans, bank operations) but is not a competitive player and must not appear
/// in public-facing leaderboards or rankings.
/// </summary>
public static class GovernmentActorConstants
{
    /// <summary>
    /// The canonical email address of the government system actor.
    /// Used to identify and filter the government player in leaderboard queries.
    /// </summary>
    public const string GovernmentEmail = "government@capitalism.game";
}
