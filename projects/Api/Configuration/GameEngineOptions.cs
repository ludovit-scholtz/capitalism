namespace Api.Configuration;

/// <summary>
/// Configuration options for the background tick-based game engine.
/// </summary>
public sealed class GameEngineOptions
{
    public const string SectionName = "GameEngine";

    /// <summary>Whether the tick engine background service is enabled.</summary>
    public bool Enabled { get; set; } = true;
}
