using GopherWoodEngine.Runtime.Modules;

namespace GopherWoodEngine.Runtime;

/// <summary>
/// Represents the configuration settings for Gopher Wood Engine.
/// </summary>
public record EngineConfig
{
    /// <summary>
    /// The display name of the game.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Design width of the game window, in pixels.
    /// </summary>
    public int Width { get; init; } = 1280;

    /// <summary>
    /// Design height of the game window, in pixels.
    /// </summary>
    public int Height { get; init; } = 720;

    /// <summary>
    /// Optional seed value for initializing the shared application <see cref="IRandomNumberGenerator"/> singleton service.
    /// </summary>
    /// <remarks>
    /// When set to a specific value, enables reproducible random number generation for debugging, testing, or replay functionality.
    /// When <c>null</c>, random number generation is non-deterministic.
    /// </remarks>
    public int? RandomSeed { get; init; } = null;
}
