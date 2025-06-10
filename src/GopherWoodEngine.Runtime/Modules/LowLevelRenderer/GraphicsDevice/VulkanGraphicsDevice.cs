using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules;

internal unsafe class VulkanGraphicsDevice : IGraphicsDevice
{
    private const uint VK_VERSION_MAJOR = 1;
    private const uint VK_VERSION_MINOR = 1;

    private readonly IWindow _silkWindow;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private bool _disposed = false;

    public VulkanGraphicsDevice(IEventSystem eventSystem, EngineConfig engineConfig)
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

        _vk = Vk.GetApi();
        _instance = CreateInstance(engineConfig);
    }

    public IInputContext GetWindowInputContext()
    {
        // Window must have been already initialized.
        return _silkWindow.CreateInput();
    }

    public void InitiateWindowMessageLoop() => _silkWindow.Run();

    public void Shutdown() => _silkWindow.Close();

    private Instance CreateInstance(EngineConfig engineConfig)
    {
        Version? engineVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version();

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi(engineConfig.Title),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Gopher Wood Engine"),
            EngineVersion = new Version32(Convert.ToUInt32(Math.Abs(engineVersion.Major)), Convert.ToUInt32(Math.Abs(engineVersion.Minor)), Convert.ToUInt32(Math.Abs(engineVersion.Revision))),
            ApiVersion = new Version32(VK_VERSION_MAJOR, VK_VERSION_MINOR, 0)
        };

        return CreateInstanceUnmanaged(appInfo);
    }

    private Instance CreateInstanceUnmanaged(ApplicationInfo appInfo)
    {
        Instance? vulkanInstance = null;

        try
        {
            byte** ppExtensions = _silkWindow.VkSurface!.GetRequiredExtensions(out uint count);

            InstanceCreateInfo createInfo = new()
            {
                SType = StructureType.InstanceCreateInfo,
                PApplicationInfo = &appInfo,
                EnabledExtensionCount = count,
                PpEnabledExtensionNames = ppExtensions,
                EnabledLayerCount = 0
            };

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
                _vk.DestroyInstance(_instance, null);
                _vk.Dispose();
                _silkWindow.Dispose();
            }

            _disposed = true;
        }
    }
}
