using GopherWoodEngine.Runtime.Modules.Rendering;
using Silk.NET.Vulkan;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines the contract for a specialized renderer that handles specific renderable types.
/// </summary>
/// <remarks>
/// Sub-renderers translate API-agnostic renderables into graphics API-specific commands.
/// </remarks>
public interface ISubRenderer
{
    /// <summary>
    /// Renders a single renderable object using the provided command buffer.
    /// </summary>
    /// <param name="camera">The camera providing view and projection information.</param>
    /// <param name="commandBuffer">The command buffer to record rendering commands into.</param>
    /// <param name="renderable">The renderable object to draw.</param>
    void Render(ICamera camera, CommandBuffer commandBuffer, IRenderable renderable);
}