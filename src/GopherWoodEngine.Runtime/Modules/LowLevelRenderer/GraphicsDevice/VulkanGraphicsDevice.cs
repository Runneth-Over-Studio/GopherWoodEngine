using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;
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
    private readonly IWindow _silkWindow;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly VulkanDebugger? _debugger;
    private readonly VulkanDevices _devices;
    private bool _enableValidationLayers = false;
    private bool _disposed = false;

    public VulkanGraphicsDevice(IEventSystem eventSystem, ILogger<IGraphicsDevice> logger, EngineConfig engineConfig)
    {
        EnableValidationLayers();

        WindowOptions options = WindowOptions.DefaultVulkan with
        {
            Title = engineConfig.Title,
            Size = new Vector2D<int>(engineConfig.Width, engineConfig.Height),
            API = GraphicsAPI.DefaultVulkan with
            {
                Version = new APIVersion(Convert.ToInt32(VK_VERSION_MAJOR), Convert.ToInt32(VK_VERSION_MINOR))
            }
        };

        _silkWindow = Window.Create(options);

        _silkWindow.Load += () => eventSystem.Publish(this, new WindowLoadEventArgs());
        _silkWindow.Update += (delta) => eventSystem.Publish(this, new WindowUpdateEventArgs(delta));
        _silkWindow.Render += (delta) => eventSystem.Publish(this, new WindowRenderEventArgs(delta));
        _silkWindow.Resize += (size) => eventSystem.Publish(this, new WindowResizeEventArgs(size.X, size.Y));
        _silkWindow.FramebufferResize += (size) => eventSystem.Publish(this, new WindowFramebufferResizeEventArgs(size.X, size.Y));
        _silkWindow.FocusChanged += (focused) => eventSystem.Publish(this, new WindowFocusChangedEventArgs(focused));
        _silkWindow.Closing += () => eventSystem.Publish(this, new WindowCloseEventArgs());

        _silkWindow.Initialize();

        if (_silkWindow.VkSurface is null)
        {
            throw new PlatformNotSupportedException("Windowing platform doesn't support Vulkan.");
        }

        _logger = logger;
        _vk = Vk.GetApi();
        _instance = CreateInstance(engineConfig);
        _debugger = !_enableValidationLayers ? null : new VulkanDebugger(_instance, _vk, logger);
        _devices = new VulkanDevices(_silkWindow, _instance, _vk, _enableValidationLayers);
    }

    public IInputContext GetWindowInputContext() => _silkWindow.CreateInput();

    public void InitiateWindowMessageLoop() => _silkWindow.Run();

    public void Shutdown() => _silkWindow.Close();

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
            byte** glfwExtensions = _silkWindow.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount);
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
                _devices.Dispose();
                _debugger?.Dispose();
                _vk.DestroyInstance(_instance, null);
                _vk.Dispose();
                _silkWindow.Dispose();
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
