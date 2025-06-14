using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;

internal unsafe class VulkanDevices : IDisposable
{
    internal PhysicalDevice PhysicalDevice { get; }
    internal Device LogicalDevice { get; }
    internal Queue GraphicsQueue { get; }
    internal Queue PresentQueue { get; }

    private readonly Vk _vk;
    private bool _disposed = false;

    public VulkanDevices(Instance instance, Vk vk, VulkanSurface surface, bool enableValidationLayers)
    {
        _vk = vk;

        (PhysicalDevice physicalDevice, QueueFamilyIndices indices) = PickPhysicalDevice(instance, vk, surface);
        if (indices.GraphicsIndex == null || indices.PresentIndex == null)
        {
            throw new Exception("Queue family indices are not complete. Ensure the physical device supports graphics and presentation queues.");
        }

        PhysicalDevice = physicalDevice;
        LogicalDevice = CreateLogicalDevice(vk, physicalDevice, indices.GraphicsIndex.Value, indices.PresentIndex.Value, surface, enableValidationLayers);

        vk.GetDeviceQueue(LogicalDevice, indices.GraphicsIndex.Value, 0, out Queue graphicsQueue);
        vk.GetDeviceQueue(LogicalDevice, indices.PresentIndex.Value, 0, out Queue presentQueue);
        GraphicsQueue = graphicsQueue;
        PresentQueue = presentQueue;
    }

    internal static QueueFamilyIndices FindQueueFamilies(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
    {
        QueueFamilyIndices queueFamilyIndices = new();

        uint queueFamilityCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilityCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilityCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilityCount, queueFamiliesPtr);
        }

        uint i = 0;
        foreach (QueueFamilyProperties queueFamily in queueFamilies)
        {
            if (queueFamilyIndices.GraphicsIndex == null && queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                queueFamilyIndices.GraphicsIndex = i;
            }

            if (queueFamilyIndices.PresentIndex == null && surface.PresentIsSupported(physicalDevice, i))
            {
                queueFamilyIndices.PresentIndex = i;
            }

            if (queueFamilyIndices.IsComplete)
            {
                break;
            }

            i++;
        }

        return queueFamilyIndices;
    }

    private static string[] GetDeviceExtensions()
    {
        return [KhrSwapchain.ExtensionName];
    }

    private static (PhysicalDevice, QueueFamilyIndices) PickPhysicalDevice(Instance instance, Vk vk, VulkanSurface surface)
    {
        PhysicalDevice? physicalDevice = null;
        QueueFamilyIndices? queueFamily = null;

        IReadOnlyCollection<PhysicalDevice> devices = vk.GetPhysicalDevices(instance);

        foreach (PhysicalDevice device in devices)
        {
            queueFamily = IsDeviceSuitable(vk, device, surface);
            if (queueFamily != null)
            {
                physicalDevice = device;
                break;
            }
        }

        if (queueFamily == null || physicalDevice == null || physicalDevice.Value.Handle == 0)
        {
            throw new Exception("Failed to find a suitable GPU.");
        }

        return (physicalDevice.Value, queueFamily.Value);
    }

    private static QueueFamilyIndices? IsDeviceSuitable(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
    {
        bool extensionsSupported = CheckDeviceExtensionsSupport(vk, physicalDevice);

        bool swapChainAdequate = false;
        if (extensionsSupported)
        {
            SwapChainSupport swapChainSupport = surface.GetSwapChainSupport(physicalDevice);
            swapChainAdequate = swapChainSupport.Formats.Length != 0 && swapChainSupport.PresentModes.Length != 0;
        }

        if (extensionsSupported && swapChainAdequate)
        {
            return FindQueueFamilies(vk, physicalDevice, surface);
        }

        return null;
    }

    private static bool CheckDeviceExtensionsSupport(Vk vk, PhysicalDevice physicalDevice)
    {
        uint extensionsCount = 0;
        vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, ref extensionsCount, null);

        ExtensionProperties[] availableExtensions = new ExtensionProperties[extensionsCount];
        fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
        {
            vk.EnumerateDeviceExtensionProperties(physicalDevice, (byte*)null, ref extensionsCount, availableExtensionsPtr);
        }

        HashSet<string?> availableExtensionNames = [.. availableExtensions.Select(extension => Marshal.PtrToStringAnsi((IntPtr)extension.ExtensionName))];

        return GetDeviceExtensions().All(availableExtensionNames.Contains);
    }

    private static Device CreateLogicalDevice(Vk vk, PhysicalDevice physicalDevice, uint graphicsIndex, uint presentIndex, VulkanSurface surface, bool enableValidationLayers)
    {
        uint[] uniqueQueueFamilies = [graphicsIndex, presentIndex];
        uniqueQueueFamilies = [.. uniqueQueueFamilies.Distinct()];

        using GlobalMemory globalMemory = GlobalMemory.Allocate(uniqueQueueFamilies.Length * sizeof(DeviceQueueCreateInfo));
        DeviceQueueCreateInfo* queueCreateInfo = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref globalMemory.GetPinnableReference());

        float queuePriority = 1.0f;
        for (int i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfo[i] = new()
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority
            };
        }

        string[] deviceExtensions = GetDeviceExtensions();
        PhysicalDeviceFeatures deviceFeatures = new();

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfo,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)deviceExtensions.Length,
            PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(deviceExtensions),
            EnabledLayerCount = 0
        };

        if (enableValidationLayers)
        {
            string[] validationLayers = VulkanDebugger.GetEnabledLayerNames();
            createInfo.EnabledLayerCount = (uint)validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
        }

        if (vk.CreateDevice(physicalDevice, in createInfo, null, out Device logicalDevice) != Result.Success)
        {
            throw new Exception("Failed to create logical device.");
        }

        if (enableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        return logicalDevice;
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
                _vk.DestroyDevice(LogicalDevice, null);
            }

            _disposed = true;
        }
    }
}

internal struct QueueFamilyIndices
{
    public uint? GraphicsIndex { get; set; }
    public uint? PresentIndex { get; set; }

    public readonly bool IsComplete => GraphicsIndex != null && PresentIndex != null;
}
