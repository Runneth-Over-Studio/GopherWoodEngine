﻿using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanDevices : IDisposable
{
    // Represents a physical device (GPU) that supports Vulkan as well as other defined features.
    internal PhysicalDevice PhysicalDevice { get; }

    // Used to interface with the physical device, allowing for resource management and command submission.
    internal Device LogicalDevice { get; }

    // Indices of the queue families that are supported by the physical device.
    // Queue families allocate VkQueues, which have operations submitted to them to be asynchronously executed.
    internal QueueFamilyIndices QueueFamilyIndices { get; }

    // Graphics processing queue used for executing GPU commands.
    // Command buffers on multiple threads can all be submited at once on the main thread with a single low-overhead call.
    internal Queue GraphicsQueue { get; }

    // Queue used to manage the presentation of items.
    // Command buffers on multiple threads can all be submited at once on the main thread with a single low-overhead call.
    internal Queue PresentQueue { get; }

    private readonly Vk _vk;
    private bool _disposed = false;

    public VulkanDevices(Vk vk, Instance instance, VulkanSurface surface, bool enableValidationLayers)
    {
        _vk = vk;

        (PhysicalDevice physicalDevice, QueueFamilyIndices indices) = SelectPhysicalDevice(instance, vk, surface);

        PhysicalDevice = physicalDevice;
        QueueFamilyIndices = indices;
        LogicalDevice = CreateLogicalDevice(vk, physicalDevice, indices.GraphicsIndex, indices.PresentIndex, surface, enableValidationLayers);

        vk.GetDeviceQueue(LogicalDevice, indices.GraphicsIndex, 0, out Queue graphicsQueue);
        vk.GetDeviceQueue(LogicalDevice, indices.PresentIndex, 0, out Queue presentQueue);
        GraphicsQueue = graphicsQueue;
        PresentQueue = presentQueue;
    }

    private static string[] GetRequiredDeviceExtensions()
    {
        return [KhrSwapchain.ExtensionName];
    }

    private static (PhysicalDevice, QueueFamilyIndices) SelectPhysicalDevice(Instance instance, Vk vk, VulkanSurface surface)
    {
        PhysicalDevice? physicalDevice = null;
        QueueFamilyIndices? queueFamily = null;

        foreach (PhysicalDevice device in vk.GetPhysicalDevices(instance))
        {
            if (IsDeviceSuitable(vk, device, surface))
            {
                queueFamily = FindQueueFamilies(vk, device, surface);

                if (queueFamily != null)
                {
                    physicalDevice = device;
                    break;
                }
            }
        }

        if (queueFamily == null || physicalDevice == null || physicalDevice.Value.Handle == 0)
        {
            throw new Exception("Failed to find a suitable GPU.");
        }

        return (physicalDevice.Value, queueFamily.Value);
    }

    private static bool IsDeviceSuitable(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
    {
        bool extensionsSupported = CheckDeviceExtensionsSupport(vk, physicalDevice);

        bool swapChainAdequate = false;
        if (extensionsSupported)
        {
            SwapChainSupport swapChainSupport = surface.GetSwapChainSupport(physicalDevice);
            swapChainAdequate = swapChainSupport.Formats.Length != 0 && swapChainSupport.PresentModes.Length != 0;
        }

        return extensionsSupported && swapChainAdequate;
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

        HashSet<string?> availableExtensionNames = [.. availableExtensions.Select(extension => Marshal.PtrToStringAnsi((nint)extension.ExtensionName))];

        return GetRequiredDeviceExtensions().All(availableExtensionNames.Contains);
    }

    private static QueueFamilyIndices FindQueueFamilies(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
    {
        uint? graphicsIndex = null;
        uint? presentIndex = null;
        uint? computeIndex = null;
        uint? transferIndex = null;

        uint queueFamilityCount = 0;
        vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilityCount, null);

        QueueFamilyProperties[] queueFamilies = new QueueFamilyProperties[queueFamilityCount];
        fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
        {
            vk.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, ref queueFamilityCount, queueFamiliesPtr);
        }

        uint i = 0;
        HashSet<uint> usedIndices = [];
        foreach (QueueFamilyProperties queueFamily in queueFamilies)
        {
            if (graphicsIndex == null && queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                graphicsIndex = i;
                usedIndices.Add(i);
            }

            if ((computeIndex == null || !usedIndices.Contains(i)) && queueFamily.QueueFlags.HasFlag(QueueFlags.ComputeBit))
            {
                computeIndex = i;
                usedIndices.Add(i);
            }

            if ((presentIndex == null || !usedIndices.Contains(i)) && surface.PresentIsSupported(physicalDevice, i))
            {
                presentIndex = i;
                usedIndices.Add(i);
            }

            if ((transferIndex == null || !usedIndices.Contains(i)) && queueFamily.QueueFlags.HasFlag(QueueFlags.TransferBit))
            {
                transferIndex = i;
                usedIndices.Add(i);
            }

            i++;
        }

        if (graphicsIndex == null || presentIndex == null || computeIndex == null || transferIndex == null)
        {
            throw new Exception("Failed to find required queue families.");
        }

        return new QueueFamilyIndices()
        {
            GraphicsIndex = graphicsIndex.Value,
            PresentIndex = presentIndex.Value,
            ComputeIndex = computeIndex.Value,
            TransferIndex = transferIndex.Value
        };
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

        string[] deviceExtensions = GetRequiredDeviceExtensions();
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
    public uint GraphicsIndex { get; set; }
    public uint PresentIndex { get; set; }
    public uint ComputeIndex { get; set; }
    public uint TransferIndex { get; set; }
}
