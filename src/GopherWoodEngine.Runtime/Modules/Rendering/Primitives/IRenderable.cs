using System;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents any game object that can be rendered to the screen.
/// </summary>
/// <remarks>
/// Renderables are API-agnostic representations of visual game elements.
/// They are converted into graphics API-specific commands by the renderer backend.
/// </remarks>
public interface IRenderable
{
    /// <summary>
    /// Gets the unique identifier for this renderable.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the transform information for positioning, rotating, and scaling this renderable.
    /// </summary>
    Transform Transform { get; }

    /// <summary>
    /// Gets whether this renderable should be drawn this frame.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Gets the render layer/order hint for sorting.
    /// </summary>
    int RenderLayer { get; }
}