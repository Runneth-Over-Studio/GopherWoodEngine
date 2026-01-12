using Silk.NET.Vulkan;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines the contract for a renderer that can execute rendering commands.
/// </summary>
/// <remarks>
/// Renderers are registered with the <see cref="IRenderer"/> and execute their
/// rendering logic during the main render pass. Each renderer is responsible for
/// binding its own pipeline, descriptor sets, and issuing draw commands.
/// </remarks>
public interface ISubRenderer
{
    /// <summary>
    /// Gets the render order priority for this renderer.
    /// </summary>
    /// <remarks>
    /// Lower values execute first. This allows control over rendering order for
    /// features like opaque geometry before transparent geometry, or skybox before scene.
    /// Common values: Skybox=0, Opaque=100, Transparent=200, UI=1000.
    /// </remarks>
    int RenderOrder { get; }

    /// <summary>
    /// Executes rendering commands for this renderer.
    /// </summary>
    /// <param name="camera">The camera providing view and projection information for rendering.</param>
    /// <param name="commandBuffer">The command buffer to record rendering commands into.</param>
    /// <remarks>
    /// <para>
    /// This method is called within an active render pass. The renderer should:
    /// </para>
    /// <list type="number">
    /// <item><description>Bind its pipeline</description></item>
    /// <item><description>Bind descriptor sets (uniforms, textures, etc.)</description></item>
    /// <item><description>Bind vertex/index buffers</description></item>
    /// <item><description>Issue draw commands</description></item>
    /// </list>
    /// <para>
    /// Do not begin/end render passes within this method - that is handled by the caller.
    /// </para>
    /// </remarks>
    void Render(ICamera camera, CommandBuffer commandBuffer);
}