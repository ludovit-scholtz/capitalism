namespace Api.Engine;

/// <summary>
/// Represents a single processing phase within a game tick.
/// Phases are executed in <see cref="Order"/> sequence by <see cref="TickProcessor"/>.
/// </summary>
public interface ITickPhase
{
    /// <summary>Human-readable name for logging.</summary>
    string Name { get; }

    /// <summary>Execution priority (lower runs first).</summary>
    int Order { get; }

    /// <summary>Process this phase using pre-loaded data in the context.</summary>
    Task ProcessAsync(TickContext context);
}
