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
    private readonly IWindow _silkWindow;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly VulkanDebugger? _debugger;
    private readonly VulkanSurface _surface;
    private readonly VulkanDevices _devices;
    private readonly VulkanSwapChain _swapChain;
    private readonly VulkanPipeline _pipeline;
    private readonly CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers;
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
        _silkWindow.Initialize();

        if (_silkWindow.VkSurface is null)
        {
            throw new PlatformNotSupportedException("Windowing platform doesn't support Vulkan.");
        }

        _logger = logger;
        _vk = Vk.GetApi();
        _instance = CreateInstance(engineConfig);
        _debugger = !_enableValidationLayers ? null : new VulkanDebugger(_instance, _vk, logger);
        _surface = new VulkanSurface(_silkWindow, _instance, _vk);
        _devices = new VulkanDevices(_instance, _vk, _surface, _enableValidationLayers);
        _swapChain = new VulkanSwapChain(_instance, _vk, _surface, _devices, _silkWindow.FramebufferSize);
        _pipeline = new VulkanPipeline(_vk, _devices.LogicalDevice, _swapChain);
        _commandPool = CreateCommandPool();
        _commandBuffers = CreateCommandBuffers();

        LogGraphicsDeviceInfo();
    }

    public void HookWindowEvents(IEventSystem eventSystem)
    {
        _silkWindow.Load += () => eventSystem.Publish(this, new WindowLoadEventArgs());
        _silkWindow.Update += (delta) => eventSystem.Publish(this, new WindowUpdateEventArgs(delta));
        _silkWindow.Render += (delta) => eventSystem.Publish(this, new WindowRenderEventArgs(delta));
        _silkWindow.Resize += (size) => eventSystem.Publish(this, new WindowResizeEventArgs(size.X, size.Y));
        _silkWindow.FramebufferResize += (size) => eventSystem.Publish(this, new WindowFramebufferResizeEventArgs(size.X, size.Y));
        _silkWindow.FocusChanged += (focused) => eventSystem.Publish(this, new WindowFocusChangedEventArgs(focused));
        _silkWindow.Closing += () => eventSystem.Publish(this, new WindowCloseEventArgs());
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

    private CommandPool CreateCommandPool()
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = _devices.QueueFamilyIndices.GraphicsIndex!.Value
        };

        if (_vk.CreateCommandPool(_devices.LogicalDevice, in poolInfo, null, out CommandPool commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool.");
        }

        return commandPool;
    }

    private CommandBuffer[] CreateCommandBuffers()
    {
        CommandBuffer[] commandBuffers = new CommandBuffer[_pipeline.Framebuffers.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
        {
            if (_vk.AllocateCommandBuffers(_devices.LogicalDevice, in allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate command buffers.");
            }
        }

        for (int i = 0; i < commandBuffers.Length; i++)
        {
            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo
            };

            if (_vk.BeginCommandBuffer(commandBuffers[i], in beginInfo) != Result.Success)
            {
                throw new Exception("Failed to begin recording command buffer.");
            }

            RenderPassBeginInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _pipeline.RenderPass,
                Framebuffer = _pipeline.Framebuffers[i],
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = _swapChain.Extent
                }
            };

            ClearValue clearColor = new()
            {
                Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 }
            };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;

            _vk.CmdBeginRenderPass(commandBuffers[i], &renderPassInfo, SubpassContents.Inline);
            _vk.CmdBindPipeline(commandBuffers[i], PipelineBindPoint.Graphics, _pipeline.GraphicsPipeline);
            _vk.CmdDraw(commandBuffers[i], 3, 1, 0, 0);
            _vk.CmdEndRenderPass(commandBuffers[i]);

            if (_vk.EndCommandBuffer(commandBuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to record command buffer.");
            }
        }

        return commandBuffers;
    }

    private void LogGraphicsDeviceInfo()
    {
        _vk.GetPhysicalDeviceProperties(_devices.PhysicalDevice, out PhysicalDeviceProperties properties);

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
        _logger.LogDebug("... Graphics Family Index: {i}", _devices.QueueFamilyIndices.GraphicsIndex?.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Compute Family Index: {i}", _devices.QueueFamilyIndices.ComputeIndex?.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Transfer Family Index: {i}", _devices.QueueFamilyIndices.TransferIndex?.ToString() ?? "<Not Found>");
        _logger.LogDebug("... Present Family Index: {i}", _devices.QueueFamilyIndices.PresentIndex?.ToString() ?? "<Not Found>");
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
                _vk.DestroyCommandPool(_devices.LogicalDevice, _commandPool, null);
                _pipeline.Dispose();
                _swapChain.Dispose();
                _devices.Dispose();
                _surface.Dispose();
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
