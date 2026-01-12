using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Vulkan-based implementation of the graphics device interface.
/// </summary>
/// <remarks>
/// <para>
/// This class manages the Vulkan rendering infrastructure, including device initialization,
/// surface management, swapchain coordination, and debug validation layers. It provides
/// the foundation for Vulkan-based rendering in the engine.
/// </para>
/// <para>
/// Debug validation layers are automatically enabled in debug builds and disabled in
/// release builds for optimal performance.
/// </para>
/// </remarks>
public unsafe sealed class VulkanGraphicsDeviceInterface : IGraphicsDeviceInterface
{
    /// <summary>
    /// Gets the Vulkan API wrapper that provides access to core Vulkan functions and the instance.
    /// </summary>
    internal VulkanAPI VulkanAPI { get; }

    /// <summary>
    /// Gets the Vulkan devices manager responsible for physical and logical device management.
    /// </summary>
    internal VulkanDevices Devices { get; }

    /// <summary>
    /// Gets the Vulkan swap-chain manager responsible for presenting rendered images to the screen.
    /// </summary>
    internal VulkanSwapChainNew SwapChain { get; }

    private readonly ILogger<IGraphicsDeviceInterface> _logger;
    private readonly VulkanDebugger? _debugger;
    private readonly VulkanSurface _surface;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanGraphicsDeviceInterface"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="loggerFactory">The logger factory for creating the VulkanDebugger logger.</param>
    /// <param name="window">The window that will be used for rendering.</param>
    /// <param name="eventSystem">The event system for subscribing to window events.</param>
    /// <param name="engineConfig">The engine configuration settings.</param>
    /// <remarks>
    /// <para>
    /// This constructor performs the complete Vulkan initialization sequence:
    /// </para>
    /// <list type="number">
    /// <item><description>Creates the Vulkan instance with required extensions</description></item>
    /// <item><description>Sets up debug validation layers (debug builds only)</description></item>
    /// <item><description>Creates the virtual surface for presentation</description></item>
    /// <item><description>Selects and initializes the best available GPU</description></item>
    /// <item><description>Creates the swapchain for frame presentation</description></item>
    /// </list>
    /// </remarks>
    public VulkanGraphicsDeviceInterface(ILogger<IGraphicsDeviceInterface> logger, ILoggerFactory loggerFactory, IWindow window, IEventSystem eventSystem, EngineConfig engineConfig)
    {
        ILogger<VulkanDebugger> vkLogger = loggerFactory.CreateLogger<VulkanDebugger>();

        _logger = logger;
        VulkanAPI = new VulkanAPI(vkLogger, window.VkSurface!, engineConfig);
        _debugger = VulkanAPI.ValidationLayersEnabled ? new VulkanDebugger(VulkanAPI, vkLogger) : null;
        _surface = new VulkanSurface(window.VkSurface!, VulkanAPI);
        Devices = new VulkanDevices(VulkanAPI, _surface);
        SwapChain = new VulkanSwapChainNew(window, VulkanAPI, _surface, Devices);

        LogGraphicsDeviceInfo();

        eventSystem.Subscribe<WindowResizeEventArgs>((s, e) => SwapChain.OnResize(s, e));
    }

    /// <inheritdoc/>
    public void WaitIdle()
    {
        VulkanAPI.Vk.DeviceWaitIdle(Devices.LogicalDevice);
    }

    private void LogGraphicsDeviceInfo()
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        PhysicalDeviceProperties properties = Devices.PhysicalDeviceSpecs.PhysicalDeviceProperties;

        int driverMajor = (int)((properties.DriverVersion >> 22) & 0x3FF);
        int driverMinor = (int)((properties.DriverVersion >> 12) & 0x3FF);
        int driverPatch = (int)(properties.DriverVersion & 0xFFF);

        int vulkanMajor = (int)((properties.ApiVersion >> 22) & 0x3FF);
        int vulkanMinor = (int)((properties.ApiVersion >> 12) & 0x3FF);
        int vulkanPatch = (int)(properties.ApiVersion & 0xFFF);

        _logger.LogDebug("GRAPHICS DEVICE:");
        _logger.LogDebug("... Device Name: {name}", SilkMarshal.PtrToString((nint)properties.DeviceName) ?? "<Unknown>");
        _logger.LogDebug("... Device Type: {type}", properties.DeviceType);
        _logger.LogDebug("... GPU Driver Version: {v}", $"{driverMajor}.{driverMinor}.{driverPatch}");
        _logger.LogDebug("... Vulkan Version: {v}", $"{vulkanMajor}.{vulkanMinor}.{vulkanPatch}");
        _logger.LogDebug("... Graphics Family Index: {i}", Devices.QueueFamilyIndices.GraphicsIndex.ToString());
        _logger.LogDebug("... Present Family Index: {i}", Devices.QueueFamilyIndices.PresentIndex.ToString());
        _logger.LogDebug("... Compute Family Index: {i}", Devices.QueueFamilyIndices.ComputeIndex?.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Transfer Family Index: {i}", Devices.QueueFamilyIndices.TransferIndex?.ToString() ?? "<Not Found>");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        WaitIdle();

        SwapChain.Dispose();
        Devices.Dispose();
        _surface.Dispose();
        _debugger?.Dispose();
        VulkanAPI.Dispose();
    }
}
