using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using GopherWoodEngine.Runtime.Modules.Rendering;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Vulkan-based implementation of the main renderer.
/// </summary>
/// <remarks>
/// <para>
/// This class orchestrates the Vulkan rendering pipeline by managing the frame lifecycle,
/// render pass execution, and coordination of registered sub-renderers. It handles swapchain
/// image acquisition, command buffer recording, and presentation to the screen.
/// </para>
/// <para>
/// Sub-renderers can be dynamically registered and are executed in priority order during
/// each frame's render pass. The renderer automatically handles swapchain recreation when
/// necessary (e.g., window resize) and ensures proper synchronization of GPU resources.
/// </para>
/// </remarks>
public sealed class VulkanRenderer : IRenderer
{
    private readonly VulkanGraphicsDeviceInterface _vkInterface;
    private readonly Dictionary<Type, ISubRenderer> _subRenderersByType = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanRenderer"/> class.
    /// </summary>
    /// <param name="vkInterface">The Vulkan graphics device interface to use for rendering.</param>
    /// <remarks>
    /// The Vulkan graphics device interface provides access to Vulkan-specific resources
    /// such as the swapchain, command buffers, and device queues required for rendering.
    /// </remarks>
    public VulkanRenderer(VulkanGraphicsDeviceInterface vkInterface)
    {
        _vkInterface = vkInterface;

        // Register sub-renderers for different renderable types
        RegisterSubRenderer(typeof(Mesh), new VulkanMeshSubRenderer(_vkInterface));
        //RegisterSubRenderer(typeof(Sprite), new VulkanSpriteSubRenderer(_vkInterface));
    }

    private void RegisterSubRenderer(Type renderableType, ISubRenderer subRenderer)
    {
        _subRenderersByType[renderableType] = subRenderer;
    }

    /// <inheritdoc/>
    public void Render(ICamera camera, RenderContext renderContext)
    {
        VulkanFrame frame = _vkInterface.SwapChain.GetNextFrame();
        CommandBuffer? cmd = frame.Begin();

        if (!cmd.HasValue)
        {
            // Swapchain needs recreation, skip this frame
            return;
        }

        _vkInterface.SwapChain.BeginRenderPass(cmd.Value);

        ExecuteSubRenderers(camera, renderContext, cmd.Value);

        _vkInterface.SwapChain.EndRenderPass(cmd.Value);
        frame.End();
        renderContext.Clear();
    }

    private void ExecuteSubRenderers(ICamera camera, RenderContext renderContext, CommandBuffer cmd)
    {
        var sortedGroups = renderContext.GetRenderables()
            .GroupBy(r => r.RenderLayer)
            .OrderBy(g => g.Key);

        foreach (var layerGroup in sortedGroups)
        {
            var typeGroups = layerGroup.GroupBy(r => r.GetType());

            foreach (var typeGroup in typeGroups)
            {
                if (_subRenderersByType.TryGetValue(typeGroup.Key, out ISubRenderer? subRenderer))
                {
                    foreach (IRenderable renderable in typeGroup)
                    {
                        subRenderer.Render(camera, cmd, renderable);
                    }
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (ISubRenderer renderer in _subRenderersByType.Values)
        {
            if (renderer is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _subRenderersByType.Clear();
        _vkInterface.Dispose();
    }
}
