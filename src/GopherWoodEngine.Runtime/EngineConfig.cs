using System;

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
}
