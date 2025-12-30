using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Vulkan;
using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VirtualScreen.Vulkan;
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
using System.Reflection;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules;

internal unsafe sealed class VulkanVirtualScreen : IVirtualScreen
{
    private const uint VK_VERSION_MAJOR = 1;
    private const uint VK_VERSION_MINOR = 3;

    internal IWindow Window { get; }
    internal Vk Vk { get; }
    internal Instance Instance { get; }
    internal VulkanSurface Surface { get; }
    internal bool ValidationLayersEnabled { get; private set; } = false;

    private bool _isDisposed = false;

    public VulkanVirtualScreen(ILogger<VulkanDebugger> vkLogger, EngineConfig engineConfig)
    {
        EnableValidationLayers();

        Window = CreateWindow(engineConfig);
        Vk = Vk.GetApi();
        Instance = CreateInstance(vkLogger, engineConfig);
        Surface = new VulkanSurface(Window, Vk, Instance);
    }

    public void HookWindowEvents(IEventSystem eventSystem)
    {
        Window.Load += () => eventSystem.Publish(this, new WindowLoadEventArgs());
        Window.Update += (delta) => eventSystem.Publish(this, new WindowUpdateEventArgs(delta));
        Window.Render += (delta) => eventSystem.Publish(this, new WindowRenderEventArgs(delta));
        Window.Resize += (size) => eventSystem.Publish(this, new WindowResizeEventArgs(size.X, size.Y));
        Window.FramebufferResize += (size) => eventSystem.Publish(this, new WindowFramebufferResizeEventArgs(size.X, size.Y));
        Window.FocusChanged += (focused) => eventSystem.Publish(this, new WindowFocusChangedEventArgs(focused));
        Window.Closing += () => eventSystem.Publish(this, new WindowCloseEventArgs());
    }

    public IInputContext CreateWindowInputContext() => Window.CreateInput();

    public void RunWindowMessageLoop()
    {
        Window.Run();
    }

    public void Shutdown() => Window.Close();

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

        IWindow window = Silk.NET.Windowing.Window.Create(options);
        window.Initialize();

        if (window.VkSurface is null)
        {
            throw new PlatformNotSupportedException("Windowing platform doesn't support Vulkan.");
        }

        return window;
    }

    private Instance CreateInstance(ILogger<VulkanDebugger> vkLogger, EngineConfig engineConfig)
    {
        Instance? vulkanInstance = null;
        Version engineVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
        InstanceCreateInfo createInfo = new();

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(engineConfig.Title),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Gopher Wood Engine"),
            EngineVersion = new Version32(Convert.ToUInt32(Math.Abs(engineVersion.Major)), Convert.ToUInt32(Math.Abs(engineVersion.Minor)), Convert.ToUInt32(Math.Abs(engineVersion.Revision))),
            ApiVersion = new Version32(VK_VERSION_MAJOR, VK_VERSION_MINOR, 0)
        };

        try
        {
            byte** glfwExtensions = Window.VkSurface!.GetRequiredExtensions(out uint glfwExtensionCount);
            string[] extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
            if (ValidationLayersEnabled)
            {
                extensions = [.. extensions, ExtDebugUtils.ExtensionName];
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

            if (ValidationLayersEnabled)
            {
                VulkanDebugger.CheckValidationLayerSupport(Vk, $"{VK_VERSION_MAJOR}.{VK_VERSION_MINOR}");
                string[] validationLayers = VulkanDebugger.GetEnabledLayerNames();
                DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
                VulkanDebugger.PopulateDebugMessengerCreateInfo(ref debugCreateInfo, vkLogger);
                createInfo.PNext = &debugCreateInfo;
                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
            }

            if (Vk.CreateInstance(in createInfo, null, out Instance instance) != Result.Success)
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

            if (ValidationLayersEnabled)
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

    internal void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                Surface.Dispose();
                Vk.DestroyInstance(Instance, null);
                Vk.Dispose();
                Window.Dispose();
            }

            _isDisposed = true;
        }
    }

    [Conditional("DEBUG")]
    private void EnableValidationLayers()
    {
        ValidationLayersEnabled = true;
    }
}
