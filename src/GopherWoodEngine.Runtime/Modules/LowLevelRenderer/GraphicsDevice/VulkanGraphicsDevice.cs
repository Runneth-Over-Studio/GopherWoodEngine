using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Vulkan;
using Microsoft.Extensions.Logging;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;

namespace GopherWoodEngine.Runtime.Modules;

internal unsafe sealed class VulkanGraphicsDevice : IGraphicsDevice
{
    private readonly ILogger<IGraphicsDevice> _logger;
    private readonly VulkanVirtualScreen _virtualScreen;
    private readonly VulkanDebugger? _debugger;
    private readonly VulkanPresenter _presenter;
    private bool _disposed = false;
    

    public VulkanGraphicsDevice(ILogger<IGraphicsDevice> logger, ILogger<VulkanDebugger> vkLogger, IVirtualScreen virtualScreen, IEventSystem eventSystem)
    {
        if (virtualScreen is VulkanVirtualScreen vulkanVirtualScreen == false)
        {
            throw new InvalidOperationException("VulkanGraphicsDevice requires a VulkanVirtualScreen instance.");
        }

        _logger = logger;
        _virtualScreen = vulkanVirtualScreen;
        _debugger = _virtualScreen.ValidationLayersEnabled ? new VulkanDebugger(_virtualScreen.Instance, _virtualScreen.Vk, vkLogger) : null;
        _presenter = new VulkanPresenter(_virtualScreen);

        LogGraphicsDeviceInfo();

        eventSystem.Subscribe<WindowRenderEventArgs>((s, e) => _presenter.DrawFrame(e.DeltaTime));
    }

    public void WaitIdle()
    {
        _virtualScreen.Vk.DeviceWaitIdle(_presenter.Devices.LogicalDevice);
    }

    private void LogGraphicsDeviceInfo()
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        _virtualScreen.Vk.GetPhysicalDeviceProperties(_presenter.Devices.PhysicalDevice, out PhysicalDeviceProperties properties);

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
        if (!_disposed)
        {
            if (disposing)
            {
                WaitIdle();

                _debugger?.Dispose();
                _presenter.Dispose();
                _virtualScreen.Dispose();
            }

            _disposed = true;
        }
    }
}
