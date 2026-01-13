using GopherWoodEngine.Runtime.Modules.Rendering;
using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines the contract for the main renderer responsible for rendering submitted primitives.
/// </summary>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Executes the render loop for a single frame, rendering all submitted primitives.
    /// </summary>
    /// <param name="camera">The camera providing view and projection information.</param>
    /// <param name="renderContext">The context containing all renderables to draw this frame.</param>
    void Render(ICamera camera, RenderContext renderContext);
}
