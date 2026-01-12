using GopherWoodEngine.Runtime.Modules;
using Silk.NET.Core;

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
    /// Optional icon image to display in the window title bar and taskbar.
    /// </summary>
    /// <remarks>
    /// When set, this <see cref="RawImage"/> is used as the application window icon.
    /// When <c>null</c>, the platform's default window icon is used.
    /// The image data should be in RGBA format with appropriate dimensions (typically 16x16, 32x32, or 48x48 pixels).
    /// </remarks>
    public RawImage? WindowIcon { get; init; } = null;

    /// <summary>
    /// Optional seed value for initializing the shared application <see cref="IRandomNumberGenerator"/> singleton service.
    /// </summary>
    /// <remarks>
    /// When set to a specific value, enables reproducible random number generation for debugging, testing, or replay functionality.
    /// When <c>null</c>, random number generation is non-deterministic.
    /// </remarks>
    public int? RandomSeed { get; init; } = null;
}
