using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VulkanTutorial.GraphicsDeviceInterface.VulkanBackend;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VulkanTutorial.GraphicsDeviceInterface;

internal unsafe sealed class VulkanGraphicsDeviceInterface : IGraphicsDeviceInterface
{
    private readonly ILogger<IGraphicsDeviceInterface> _logger;
    private readonly VulkanAPI _vulkanAPI;
    private readonly VulkanSurface _surface;
    private readonly VulkanDebugger? _debugger;
    private VulkanPresenter? _presenter;
    private bool _isDisposed = false;

    public VulkanGraphicsDeviceInterface(ILogger<IGraphicsDeviceInterface> logger, ILogger<VulkanDebugger> vkLogger, IWindow window, IEventSystem eventSystem, EngineConfig engineConfig)
    {
        _logger = logger;
        _vulkanAPI = new VulkanAPI(vkLogger, window.VkSurface!, engineConfig);
        _surface = new VulkanSurface(window.VkSurface!, _vulkanAPI);
        _presenter = new VulkanPresenter(window, _vulkanAPI, _surface);
        _debugger = _vulkanAPI.ValidationLayersEnabled ? new VulkanDebugger(_vulkanAPI, vkLogger) : null;

        LogGraphicsDeviceInfo();

        eventSystem.Subscribe<WindowRenderEventArgs>((s, e) => _presenter?.DrawFrame(e.DeltaTime));
    }

    public void WaitIdle()
    {
        if (_presenter != null)
        {
            _vulkanAPI.Vk.DeviceWaitIdle(_presenter.Devices.LogicalDevice);
        }
    }

    private void LogGraphicsDeviceInfo()
    {
        if (!_logger.IsEnabled(LogLevel.Debug) || _presenter == null)
        {
            return;
        }

        _vulkanAPI.Vk.GetPhysicalDeviceProperties(_presenter.Devices.PhysicalDevice, out PhysicalDeviceProperties properties);

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
        _logger.LogDebug("... Graphics Family Index: {i}", _presenter.Devices.QueueFamilyIndices.GraphicsIndex.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Present Family Index: {i}", _presenter.Devices.QueueFamilyIndices.PresentIndex.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Compute Family Index: {i}", _presenter.Devices.QueueFamilyIndices.ComputeIndex?.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Transfer Family Index: {i}", _presenter.Devices.QueueFamilyIndices.TransferIndex?.ToString() ?? "<Not Found>");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                WaitIdle();

                _debugger?.Dispose();
                _presenter?.Dispose();
                _surface.Dispose();
                _vulkanAPI.Dispose();
            }

            _isDisposed = true;
        }
    }
}
