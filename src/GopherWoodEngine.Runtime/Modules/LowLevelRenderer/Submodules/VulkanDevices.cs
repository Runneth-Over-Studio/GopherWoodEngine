using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;

internal unsafe class VulkanDevices : IDisposable
{
    internal PhysicalDevice PhysicalDevice { get; }
    internal Device LogicalDevice { get; }
    internal Queue GraphicsQueue { get; }

    private readonly Vk _vk;
    private bool _disposed = false;

    public VulkanDevices(Instance instance, Vk vk, bool enableValidationLayers)
    {
        _vk = vk;

        (uint queueFamilyIndex, PhysicalDevice physicalDevice) = PickPhysicalDevice(instance, vk);
        PhysicalDevice = physicalDevice;
        LogicalDevice = CreateLogicalDevice(queueFamilyIndex, vk, physicalDevice, enableValidationLayers);
    }

    private static (uint queueFamilyIndex, PhysicalDevice physicalDevice) PickPhysicalDevice(Instance instance, Vk vk)
    {
        uint? graphicsQueueFamilyIndex = null;
        PhysicalDevice? physicalDevice = null;

        IReadOnlyCollection<PhysicalDevice> devices = vk.GetPhysicalDevices(instance);

        foreach (PhysicalDevice device in devices)
        {
            graphicsQueueFamilyIndex = GraphicsQueueFamilyIndex(device, vk);
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

    private static uint? GraphicsQueueFamilyIndex(PhysicalDevice device, Vk vk)
    {
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
            if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                break;
            }

            i++;
        }

        return i;
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
