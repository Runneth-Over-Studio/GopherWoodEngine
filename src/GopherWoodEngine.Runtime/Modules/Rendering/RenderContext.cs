using System.Collections.Generic;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Aggregates all renderable objects to be drawn in a frame.
/// </summary>
/// <remarks>
/// Game code submits renderables to this context, which are then
/// processed by the renderer backend.
/// </remarks>
public sealed class RenderContext
{
    private readonly List<IRenderable> _renderables = [];

    /// <summary>
    /// Submits a renderable object to be drawn this frame.
    /// </summary>
    public void Submit(IRenderable renderable)
    {
        if (renderable.IsVisible)
        {
            _renderables.Add(renderable);
        }
    }

    /// <summary>
    /// Gets all submitted renderables for this frame.
    /// </summary>
    internal IReadOnlyList<IRenderable> GetRenderables() => _renderables;

    /// <summary>
    /// Clears all submitted renderables. Called at the end of each frame.
    /// </summary>
    internal void Clear()
    {
        _renderables.Clear();
    }
}
