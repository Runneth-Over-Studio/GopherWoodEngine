using Microsoft.Extensions.Logging;
using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VulkanTutorial.GraphicsDeviceInterface.VulkanBackend;

internal unsafe sealed class VulkanAPI : IDisposable
{
    internal const uint VK_VERSION_MAJOR = 1;
    internal const uint VK_VERSION_MINOR = 3;

    internal Vk Vk { get; }
    internal Instance Instance { get; }
    internal bool ValidationLayersEnabled { get; private set; } = false;

    private bool _isDisposed = false;

    public VulkanAPI(ILogger<VulkanDebugger> vkLogger, IVkSurface vkSurface, EngineConfig engineConfig)
    {
        EnableValidationLayers();

        Vk = Vk.GetApi();
        Instance = CreateInstance(vkLogger, vkSurface, Vk, ValidationLayersEnabled, engineConfig);
    }

    private static Instance CreateInstance(ILogger<VulkanDebugger> vkLogger, IVkSurface vkSurface, Vk vk, bool validationLayersEnabled, EngineConfig engineConfig)
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
            byte** glfwExtensions = vkSurface.GetRequiredExtensions(out uint glfwExtensionCount);
            string[] extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);
            if (validationLayersEnabled)
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

            if (validationLayersEnabled)
            {
                VulkanDebugger.CheckValidationLayerSupport(vk, $"{VK_VERSION_MAJOR}.{VK_VERSION_MINOR}");
                string[] validationLayers = VulkanDebugger.GetEnabledLayerNames();
                DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
                VulkanDebugger.PopulateDebugMessengerCreateInfo(ref debugCreateInfo, vkLogger);
                createInfo.PNext = &debugCreateInfo;
                createInfo.EnabledLayerCount = (uint)validationLayers.Length;
                createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
            }

            if (vk.CreateInstance(in createInfo, null, out Instance instance) != Result.Success)
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

            if (validationLayersEnabled)
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
                Vk.DestroyInstance(Instance, null);
                Vk.Dispose();
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
