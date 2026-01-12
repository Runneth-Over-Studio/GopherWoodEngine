using Silk.NET.Vulkan;

namespace GopherWoodEngine.Runtime.Modules;

public abstract class VulkanSubRendererBase : ISubRenderer
{
    /// <inheritdoc/>
    public abstract int RenderOrder { get; }

    /// <inheritdoc/>
    public abstract void Render(ICamera camera, CommandBuffer commandBuffer);

    protected VulkanGraphicsDeviceInterface VkInterface { get; }

    protected VulkanSubRendererBase()
    {
        VkInterface = Ioc.Default.GetRequiredService<VulkanGraphicsDeviceInterface>();
    }
}
