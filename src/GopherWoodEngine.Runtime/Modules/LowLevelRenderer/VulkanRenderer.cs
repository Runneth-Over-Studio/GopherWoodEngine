using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using Silk.NET.Vulkan;
using System.Collections.Generic;

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
    private readonly List<ISubRenderer> _subRenderers = [];
    private bool _subRenderersNeedsSort = false;

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
    }

    /// <inheritdoc/>
    /// <remarks>
    /// If a sub-renderer with the same instance is already registered, this method does nothing.
    /// Sub-renderers are executed in order of their <see cref="ISubRenderer.RenderOrder"/> priority.
    /// </remarks>
    public void RegisterSubRenderer(ISubRenderer renderer)
    {
        if (_subRenderers.Contains(renderer))
        {
            return;
        }

        _subRenderers.Add(renderer);
        _subRenderersNeedsSort = true;
    }

    /// <inheritdoc/>
    public bool UnregisterSubRenderer(ISubRenderer renderer)
    {
        return _subRenderers.Remove(renderer);
    }

    /// <inheritdoc/>
    public void ClearSubRenderers()
    {
        _subRenderers.Clear();
    }

    /// <inheritdoc/>
    public void Render(ICamera camera)
    {
        VulkanFrame frame = _vkInterface.SwapChain.GetNextFrame();
        CommandBuffer? cmd = frame.Begin();

        if (!cmd.HasValue)
        {
            // Swapchain needs recreation, skip this frame
            return;
        }

        _vkInterface.SwapChain.BeginRenderPass(cmd.Value);

        ExecuteSubRenderers(camera, cmd.Value);

        _vkInterface.SwapChain.EndRenderPass(cmd.Value);

        frame.End();
    }

    private void ExecuteSubRenderers(ICamera camera, CommandBuffer commandBuffer)
    {
        if (_subRenderersNeedsSort)
        {
            _subRenderers.Sort((a, b) => a.RenderOrder.CompareTo(b.RenderOrder));
            _subRenderersNeedsSort = false;
        }

        foreach (ISubRenderer renderer in _subRenderers)
        {
            renderer.Render(camera, commandBuffer);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subRenderers.Clear();
        _vkInterface.Dispose();
    }
}
