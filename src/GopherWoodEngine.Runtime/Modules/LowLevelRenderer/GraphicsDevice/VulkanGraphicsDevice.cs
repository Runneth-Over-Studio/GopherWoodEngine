using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;
using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Windowing;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules;

internal unsafe class VulkanGraphicsDevice : IGraphicsDevice
{
    private const uint VK_VERSION_MAJOR = 1;
    private const uint VK_VERSION_MINOR = 3;

    private readonly ILogger<IGraphicsDevice> _logger;
    private readonly IWindow _window;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly VulkanDebugger? _debugger;
    private readonly VulkanPresenter _presenter;
    private bool _enableValidationLayers = false;
    private bool _disposed = false;

    public VulkanGraphicsDevice(ILogger<IGraphicsDevice> logger, EngineConfig engineConfig)
    {
        EnableValidationLayers();

        _logger = logger;
        _window = CreateWindow(engineConfig);
        _vk = Vk.GetApi();
        _instance = CreateInstance(engineConfig);
        _debugger = _enableValidationLayers ? new VulkanDebugger(_instance, _vk, _logger) : null;
        _presenter = new VulkanPresenter(_window, _vk, _instance, _enableValidationLayers);

        LogGraphicsDeviceInfo();
    }

    public void HookWindowEvents(IEventSystem eventSystem)
    {
        _window.Load += () => eventSystem.Publish(this, new WindowLoadEventArgs());
        _window.Update += (delta) => eventSystem.Publish(this, new WindowUpdateEventArgs(delta));
        _window.Render += (delta) => eventSystem.Publish(this, new WindowRenderEventArgs(delta));
        _window.Resize += (size) => eventSystem.Publish(this, new WindowResizeEventArgs(size.X, size.Y));
        _window.FramebufferResize += (size) => eventSystem.Publish(this, new WindowFramebufferResizeEventArgs(size.X, size.Y));
        _window.FocusChanged += (focused) => eventSystem.Publish(this, new WindowFocusChangedEventArgs(focused));
        _window.Closing += () => eventSystem.Publish(this, new WindowCloseEventArgs());
    }

    public IInputContext CreateWindowInputContext() => _window.CreateInput();

    public void InitiateWindowMessageLoop()
    {
        _window.Render += _presenter.DrawFrame;

        _window.Run();

        _window.Render -= _presenter.DrawFrame;
        _vk.DeviceWaitIdle(_presenter.Devices.LogicalDevice);
    }

    public void Shutdown() => _window.Close();

    private static IWindow CreateWindow(EngineConfig engineConfig)
    {
        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Title = engineConfig.Title,
            Size = new Vector2D<int>(engineConfig.Width, engineConfig.Height),
            API = GraphicsAPI.DefaultVulkan with
            {
                Version = new APIVersion(Convert.ToInt32(VK_VERSION_MAJOR), Convert.ToInt32(VK_VERSION_MINOR))
            }
        };

        IWindow window = Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
        {
            throw new PlatformNotSupportedException("Windowing platform doesn't support Vulkan.");
        }

        return window;
    }

    private Instance CreateInstance(EngineConfig engineConfig)
    {
        VulkanDebugger.CheckValidationLayerSupport(_vk, $"{VK_VERSION_MAJOR}.{VK_VERSION_MINOR}");

        Instance? vulkanInstance = null;
        Version? engineVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
        InstanceCreateInfo createInfo = new();

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(engineConfig.Title),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Gopher Wood Engine"),
            EngineVersion = new Version32(Convert.ToUInt32(Math.Abs(engineVersion.Major)), Convert.ToUInt32(Math.Abs(engineVersion.Minor)), Convert.ToUInt32(Math.Abs(engineVersion.Revision))),
            ApiVersion = new Version32(VK_VERSION_MAJOR, VK_VERSION_MINOR, 0)
        };

        try
        {
            byte** glfwExtensions = _window.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount);
            string[] extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
            if (_enableValidationLayers)
            {
                extensions = extensions.Append(ExtDebugUtils.ExtensionName).ToArray();
            }

            createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = (uint)extensions.Length,
                PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions),
                EnabledLayerCount = 0,
                PNext = null
            };

            if (_enableValidationLayers)
            {
                string[] validationLayers = VulkanDebugger.GetEnabledLayerNames();
                DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
                VulkanDebugger.PopulateDebugMessengerCreateInfo(ref debugCreateInfo, _logger);
                createInfo.PNext = &debugCreateInfo;
                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
            }

            if (_vk.CreateInstance(in createInfo, null, out Instance instance) != Result.Success)
            {
                throw new Exception("Vulkan instance creation returned unsuccessfully.");
            }

            vulkanInstance = instance;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to create Vulkan instance.", ex);
        }
        finally
        {
            Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
            Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
            SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

            if (_enableValidationLayers)
            {
                SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
            }
        }

        return vulkanInstance.Value;
    }

    private void LogGraphicsDeviceInfo()
    {
        _vk.GetPhysicalDeviceProperties(_presenter.Devices.PhysicalDevice, out PhysicalDeviceProperties properties);

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
        _logger.LogDebug("... Compute Family Index: {i}", _presenter.Devices.QueueFamilyIndices.ComputeIndex.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Transfer Family Index: {i}", _presenter.Devices.QueueFamilyIndices.TransferIndex.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Present Family Index: {i}", _presenter.Devices.QueueFamilyIndices.PresentIndex.ToString() ?? "<Not Found>");
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _presenter.Dispose();
                _debugger?.Dispose();
                _vk.DestroyInstance(_instance, null);
                _vk.Dispose();
                _window.Dispose();
            }

            _disposed = true;
        }
    }

    [Conditional("DEBUG")]
    private void EnableValidationLayers()
    {
        _enableValidationLayers = true;
    }
}
