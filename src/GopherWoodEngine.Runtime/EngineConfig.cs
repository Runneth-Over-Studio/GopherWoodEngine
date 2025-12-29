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
    public required string Title { get; set; }

    /// <summary>
    /// Design width of the game window, in pixels.
    /// </summary>
    public int Width { get; set; } = 1280;

    /// <summary>
    /// Design height of the game window, in pixels.
    /// </summary>
    public int Height { get; set; } = 720;

    /// <summary>
    /// Gets or sets the seed value used to initialize a deterministic random number generator.
    /// </summary>
    /// <remarks>
    /// If the value is null, a default seed is used.
    /// To use the deterministic random number generator in your game systems,
    /// inject <see cref="IRandomNumberGenerator"/> with <c>[FromKeyedServices("Deterministic")]</c> attribute.
    /// </remarks>
    public int? RandomSeed { get; set; } = null;
}
