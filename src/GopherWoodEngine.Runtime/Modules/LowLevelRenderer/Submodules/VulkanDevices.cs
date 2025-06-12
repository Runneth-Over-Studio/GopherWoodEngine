using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;

internal unsafe class VulkanDevices : IDisposable
{
    internal PhysicalDevice PhysicalDevice { get; }
    internal Device LogicalDevice { get; }
    internal Queue GraphicsQueue { get; }
    internal KhrSurface KhrSurface { get; }
    internal SurfaceKHR SurfaceKHR { get; }

    private readonly Vk _vk;
    private readonly Instance _instance;
    private bool _disposed = false;

    public VulkanDevices(IWindow window, Instance instance, Vk vk, bool enableValidationLayers)
    {
        _vk = vk;
        _instance = instance;

        (KhrSurface khrSurface, SurfaceKHR surfaceKHR) = CreateSurface(window, instance);
        KhrSurface = khrSurface;
        SurfaceKHR = surfaceKHR;

        (uint queueFamilyIndex, PhysicalDevice physicalDevice) = PickPhysicalDevice(instance, vk, surfaceKHR, khrSurface);
        PhysicalDevice = physicalDevice;
        LogicalDevice = CreateLogicalDevice(queueFamilyIndex, vk, physicalDevice, enableValidationLayers);
    }

    private (KhrSurface, SurfaceKHR) CreateSurface(IWindow window, Instance instance)
    {
        if (!_vk.TryGetInstanceExtension<KhrSurface>(instance, out KhrSurface khrSurface))
        {
            throw new NotSupportedException("KHR_surface extension not found.");
        }

        SurfaceKHR surfaceKHR = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

        return (khrSurface, surfaceKHR);
    }

    private static (uint queueFamilyIndex, PhysicalDevice physicalDevice) PickPhysicalDevice(Instance instance, Vk vk, SurfaceKHR surfaceKHR, KhrSurface khrSurface)
    {
        uint? graphicsQueueFamilyIndex = null;
        PhysicalDevice? physicalDevice = null;

        IReadOnlyCollection<PhysicalDevice> devices = vk.GetPhysicalDevices(instance);

        foreach (PhysicalDevice device in devices)
        {
            graphicsQueueFamilyIndex = GraphicsQueueFamilyIndex(device, vk, surfaceKHR, khrSurface);
            if (graphicsQueueFamilyIndex != null)
            {
                physicalDevice = device;
                break;
            }
        }

        if (graphicsQueueFamilyIndex == null || physicalDevice == null || physicalDevice.Value.Handle == 0)
        {
            throw new Exception("Failed to find a suitable GPU.");
        }

        return (graphicsQueueFamilyIndex.Value, physicalDevice.Value);
    }

    private static Device CreateLogicalDevice(uint queueFamilyIndex, Vk vk, PhysicalDevice physicalDevice, bool enableValidationLayers)
    {
        float queuePriority = 1.0F;

        DeviceQueueCreateInfo queueCreateInfo = new()
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = queueFamilyIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        PhysicalDeviceFeatures deviceFeatures = new();

        DeviceCreateInfo createInfo = new()
        {
            SType = StructureType.DeviceCreateInfo,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &queueCreateInfo,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = 0,
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

        return logicalDevice;
    }

    private static uint? GraphicsQueueFamilyIndex(PhysicalDevice device, Vk vk, SurfaceKHR surfaceKHR, KhrSurface khrSurface)
    {
        uint? queueFamilyIndex = null;

        uint queueFamilityCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilityCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilityCount, queueFamiliesPtr);
        }

        uint i = 0;
        foreach (QueueFamilyProperties queueFamily in queueFamilies)
        {
            bool graphicsSupport = queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit);
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, surfaceKHR, out Bool32 presentSupport);

            //TODO: Not sure if this struct will ever be useful.
            QueueFamilyIndex index = new QueueFamilyIndex()
            {
                Index = i,
                HasGraphicsFamily = graphicsSupport,
                HasPresentFamily = presentSupport
            };

            if (index.IsComplete)
            {
                queueFamilyIndex = i;
                break;
            }

            i++;
        }

        return queueFamilyIndex;
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
                KhrSurface.DestroySurface(_instance, SurfaceKHR, null);
            }

            _disposed = true;
        }
    }
}

internal struct QueueFamilyIndex
{
    public uint Index { get; set; }
    public bool HasGraphicsFamily { get; set; }
    public bool HasPresentFamily { get; set; }

    public readonly bool IsComplete => HasGraphicsFamily && HasPresentFamily;
}
