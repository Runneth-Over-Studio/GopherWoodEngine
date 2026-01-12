using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

/// <summary>
/// Manages Vulkan physical and logical devices, queue families, and command pools.
/// </summary>
/// <remarks>
/// Command pools are automatically created for each available queue family, enabling efficient
/// command buffer allocation for different types of GPU operations.
/// </remarks>
internal unsafe sealed class VulkanDevices : IDisposable
{
    /// <summary>
    /// Represents a physical device (GPU) that supports Vulkan as well as other defined features.
    /// </summary>
    internal PhysicalDevice PhysicalDevice { get; }

    /// <summary>
    /// Used to interface with the physical device, allowing for resource management and command submission.
    /// </summary>
    internal Device LogicalDevice { get; }

    /// <summary>
    /// Contains comprehensive specifications and capabilities of the selected physical device.
    /// </summary>
    internal PhysicalDeviceSpecs PhysicalDeviceSpecs { get; }

    /// <summary>
    /// Indices of the queue families that are supported by the physical device.
    /// Queue families allocate VkQueues, which have operations submitted to them to be asynchronously executed.
    /// </summary>
    internal QueueFamilyIndices QueueFamilyIndices { get { return PhysicalDeviceSpecs.QueueFamilyIndices; } }

    /// <summary>
    /// Graphics processing queue used for executing GPU commands.
    /// Command buffers on multiple threads can all be submited at once on the main thread with a single low-overhead call.
    /// </summary>
    internal Queue GraphicsQueue { get; }

    /// <summary>
    /// Queue used to manage the presentation of items.
    /// Command buffers on multiple threads can all be submited at once on the main thread with a single low-overhead call.
    /// </summary>
    internal Queue PresentQueue { get; }

    /// <summary>
    /// Command pool for the graphics queue family.
    /// Used to allocate command buffers for graphics operations.
    /// </summary>
    internal CommandPool GraphicsCommandPool { get; }

    /// <summary>
    /// Command pool for the compute queue family, if available.
    /// Used to allocate command buffers for async compute operations.
    /// </summary>
    internal CommandPool? ComputeCommandPool { get; }

    /// <summary>
    /// Command pool for the transfer queue family, if available.
    /// Used to allocate command buffers for async transfer operations.
    /// </summary>
    internal CommandPool? TransferCommandPool { get; }

    private readonly Vk _vk;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanDevices"/> class.
    /// </summary>
    public VulkanDevices(VulkanAPI vulkanAPI, VulkanSurface surface)
    {
        _vk = vulkanAPI.Vk;

        (PhysicalDevice physicalDevice, PhysicalDeviceSpecs specs) = SelectBestPhysicalDevice(vulkanAPI.Instance, _vk, surface);
        PhysicalDevice = physicalDevice;
        PhysicalDeviceSpecs = specs;

        LogicalDevice = CreateLogicalDevice(_vk, physicalDevice, QueueFamilyIndices, surface, vulkanAPI.ValidationLayersEnabled);

        _vk.GetDeviceQueue(LogicalDevice, specs.QueueFamilyIndices.GraphicsIndex, 0, out Queue graphicsQueue);
        _vk.GetDeviceQueue(LogicalDevice, specs.QueueFamilyIndices.PresentIndex, 0, out Queue presentQueue);
        GraphicsQueue = graphicsQueue;
        PresentQueue = presentQueue;

        // Create command pools for each queue family
        GraphicsCommandPool = CreateCommandPool(_vk, LogicalDevice, QueueFamilyIndices.GraphicsIndex);

        // Only create compute pool if we have a dedicated compute queue
        if (QueueFamilyIndices.ComputeIndex.HasValue)
        {
            ComputeCommandPool = CreateCommandPool(_vk, LogicalDevice, QueueFamilyIndices.ComputeIndex.Value);
        }

        // Only create transfer pool if we have a dedicated transfer queue
        if (QueueFamilyIndices.TransferIndex.HasValue)
        {
            TransferCommandPool = CreateCommandPool(_vk, LogicalDevice, QueueFamilyIndices.TransferIndex.Value);
        }
    }

    /// <summary>
    /// Allocates one or more command buffers from the specified command pool.
    /// </summary>
    /// <param name="count">The number of command buffers to allocate.</param>
    /// <param name="commandPool">The command pool from which to allocate the buffers.</param>
    /// <returns>An array of allocated command buffers.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when command buffer allocation fails.
    /// </exception>
    /// <remarks>
    /// The allocated command buffers are primary-level buffers that can be submitted directly to queues.
    /// Command buffers must be freed or reset before the command pool is destroyed.
    /// </remarks>
    internal unsafe CommandBuffer[] AllocateCommandBuffers(uint count, CommandPool commandPool)
    {
        CommandBuffer[] commandBuffers = new CommandBuffer[count];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = count
        };

        if (_vk.AllocateCommandBuffers(LogicalDevice, in allocateInfo, out commandBuffers[0]) != Result.Success)
        {
            throw new InvalidOperationException("Failed to allocate command buffer(s).");
        }

        return commandBuffers;
    }

    /// <summary>
    /// Allocates and begins recording a single-use command buffer for immediate submission.
    /// </summary>
    /// <param name="commandPool">The command pool from which to allocate the command buffer.</param>
    /// <returns>A command buffer ready for recording one-time commands.</returns>
    /// <remarks>
    /// <para>
    /// This method is designed for commands that will be executed once and then discarded,
    /// such as one-time resource transfers or initialization operations.
    /// </para>
    /// <para>
    /// The command buffer is created with the <see cref="CommandBufferUsageFlags.OneTimeSubmitBit"/> flag,
    /// indicating to the driver that it will be submitted exactly once for optimization purposes.
    /// </para>
    /// <para>
    /// After recording commands, use <see cref="EndSingleUseCommandBuffer"/> to submit and free the buffer.
    /// </para>
    /// </remarks>
    internal unsafe CommandBuffer BeginSingleUseCommandBuffer(CommandPool commandPool)
    {
        CommandBuffer commandBuffer = AllocateCommandBuffers(1, commandPool)[0];

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        VulkanUtilities.AssertVk(_vk.BeginCommandBuffer(commandBuffer, in beginInfo));

        return commandBuffer;
    }

    /// <summary>
    /// Ends recording, submits, and frees a single-use command buffer.
    /// </summary>
    /// <param name="commandBuffer">The command buffer to end and submit.</param>
    /// <param name="queue">The queue to which the command buffer will be submitted.</param>
    /// <param name="commandPool">The command pool from which the buffer was allocated.</param>
    /// <remarks>
    /// <para>
    /// This is a synchronous operation that blocks until all commands have been executed.
    /// It is suitable for initialization and one-time operations but should not be used
    /// in performance-critical rendering loops.
    /// </para>
    /// <para>
    /// This method should be paired with <see cref="BeginSingleUseCommandBuffer"/> for one-time command execution patterns.
    /// </para>
    /// </remarks>
    internal unsafe void EndSingleUseCommandBuffer(CommandBuffer commandBuffer, Queue queue, CommandPool commandPool)
    {
        VulkanUtilities.AssertVk(_vk.EndCommandBuffer(commandBuffer));

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        VulkanUtilities.AssertVk(_vk.QueueSubmit(queue, 1, in submitInfo, new Fence(handle: null)));
        VulkanUtilities.AssertVk(_vk.QueueWaitIdle(queue));

        _vk.FreeCommandBuffers(LogicalDevice, commandPool, [commandBuffer]);
    }

    private static string[] GetRequiredDeviceExtensions()
    {
        return [KhrSwapchain.ExtensionName, KhrPushDescriptor.ExtensionName];
    }

    private static (PhysicalDevice, PhysicalDeviceSpecs) SelectBestPhysicalDevice(Instance instance, Vk vk, VulkanSurface surface)
    {
        List<(PhysicalDevice device, PhysicalDeviceSpecs specs, int score)> suitableDevices = [];

        foreach (PhysicalDevice device in vk.GetPhysicalDevices(instance))
        {
            if (device.Handle == 0)
            {
                continue;
            }

            if (!IsDeviceMinimallySuitable(vk, device, surface))
            {
                continue;
            }

            QueueFamilyIndices? queueFamilyIndices = FindQueueFamilies(vk, device, surface);
            if (queueFamilyIndices == null)
            {
                continue;
            }

            PhysicalDeviceProperties deviceProperties = vk.GetPhysicalDeviceProperties(device);
            PhysicalDeviceMemoryProperties memoryProperties = vk.GetPhysicalDeviceMemoryProperties(device);

            VulkanUtilities.AssertVk(surface.KhrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface.SurfaceKHR, out SurfaceCapabilitiesKHR capabilities));

            uint formatCount = 0;
            VulkanUtilities.AssertVk(surface.KhrSurface.GetPhysicalDeviceSurfaceFormats(device, surface.SurfaceKHR, ref formatCount, null));
            SurfaceFormatKHR[] formats = new SurfaceFormatKHR[formatCount];
            VulkanUtilities.AssertVk(surface.KhrSurface.GetPhysicalDeviceSurfaceFormats(device, surface.SurfaceKHR, ref formatCount, out formats[0]));

            uint presentModeCount = 0;
            VulkanUtilities.AssertVk(surface.KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface.SurfaceKHR, ref presentModeCount, null));
            PresentModeKHR[] presentModes = new PresentModeKHR[presentModeCount];
            VulkanUtilities.AssertVk(surface.KhrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface.SurfaceKHR, ref presentModeCount, out presentModes[0]));

            PhysicalDeviceSpecs specs = new()
            {
                QueueFamilyIndices = queueFamilyIndices.Value,
                PhysicalDeviceProperties = deviceProperties,
                PhysicalDeviceMemoryProperties = memoryProperties,
                SurfaceCapabilities = capabilities,
                SurfaceFormats = formats,
                PresentModes = presentModes
            };

            int score = ScorePhysicalDevice(specs);
            suitableDevices.Add((device, specs, score));
        }

        if (suitableDevices.Count == 0)
        {
            throw new Exception("Failed to find a suitable GPU.");
        }

        (PhysicalDevice bestDevice, PhysicalDeviceSpecs bestSpecs, _) = suitableDevices.OrderByDescending(d => d.score).First();

        return (bestDevice, bestSpecs);
    }

    private static bool IsDeviceMinimallySuitable(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
    {
        bool extensionsSupported = CheckDeviceExtensionsSupport(vk, physicalDevice);

        bool swapChainAdequate = false;
        if (extensionsSupported)
        {
            SwapChainSupport swapChainSupport = surface.GetSwapChainSupport(physicalDevice);
            swapChainAdequate = swapChainSupport.Formats.Length != 0 && swapChainSupport.PresentModes.Length != 0;
        }

        vk.GetPhysicalDeviceFeatures(physicalDevice, out PhysicalDeviceFeatures supportedFeatures);
        FormatProperties formatProperties = vk.GetPhysicalDeviceFormatProperties(physicalDevice, Format.R8G8B8A8Srgb);

        return extensionsSupported
            && swapChainAdequate
            && supportedFeatures.SamplerAnisotropy
            && supportedFeatures.GeometryShader
            && formatProperties.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.SampledImageFilterLinearBit);
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

    private static QueueFamilyIndices? FindQueueFamilies(Vk vk, PhysicalDevice physicalDevice, VulkanSurface surface)
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

            if ((presentIndex == null || !usedIndices.Contains(i)) && surface.PresentIsSupported(physicalDevice, i))
            {
                presentIndex = i;
                usedIndices.Add(i);
            }

            if ((computeIndex == null || !usedIndices.Contains(i)) && queueFamily.QueueFlags.HasFlag(QueueFlags.ComputeBit))
            {
                computeIndex = i;
                usedIndices.Add(i);
            }

            if ((transferIndex == null || !usedIndices.Contains(i)) && queueFamily.QueueFlags.HasFlag(QueueFlags.TransferBit))
            {
                transferIndex = i;
                usedIndices.Add(i);
            }

            i++;
        }

        if (graphicsIndex == null || presentIndex == null)
        {
            return null;
        }

        return new QueueFamilyIndices()
        {
            GraphicsIndex = graphicsIndex.Value,
            PresentIndex = presentIndex.Value,
            ComputeIndex = computeIndex,
            TransferIndex = transferIndex
        };
    }

    private static int ScorePhysicalDevice(PhysicalDeviceSpecs specs)
    {
        /*
            Scores a physical device based on its capabilities and suitability for game rendering.
            Higher scores indicate more desirable devices.
            Scoring breakdown:
            - Discrete GPU: +10,000 points (strongly preferred for gaming)
            - Integrated GPU: +1,000 points (fallback option)
            - Dedicated compute queue: +500 points (enables async compute)
            - Dedicated transfer queue: +250 points (enables async transfers)
            - Compute queue separate from graphics: +200 points (better parallelism)
            - Transfer queue separate from graphics: +100 points (better parallelism)
            - Mailbox present mode support: +50 points (triple buffering, lower latency)
            - Immediate present mode support: +25 points (for benchmarking/testing)
            - Multiple swapchain image support (>3): +30 points (smoother frame pacing)
            - SRGB format support: +20 points (preferred for proper color space)
            - VRAM capacity: +1 point per MB
            - Max texture dimension: +1 point per 1000 pixels (capped contribution)
        */

        int score = 0;

        // Strongly prefer discrete GPUs for gaming workloads
        if (specs.PhysicalDeviceProperties.DeviceType == PhysicalDeviceType.DiscreteGpu)
        {
            score += 10000;
        }
        else if (specs.PhysicalDeviceProperties.DeviceType == PhysicalDeviceType.IntegratedGpu)
        {
            score += 1000;
        }
        // Virtual GPUs, CPUs, and other types get no bonus (but aren't excluded)

        // Score based on queue family availability and separation
        // Dedicated queues allow for better parallelization and async operations

        // Having a compute queue at all is valuable for compute shaders
        if (specs.QueueFamilyIndices.ComputeIndex.HasValue)
        {
            score += 500;

            // Bonus if compute queue is on a separate queue family from graphics
            // This allows true async compute while graphics is running
            if (specs.QueueFamilyIndices.ComputeIndex.Value != specs.QueueFamilyIndices.GraphicsIndex)
            {
                score += 200;
            }
        }

        // Having a transfer queue allows for async texture/buffer uploads
        if (specs.QueueFamilyIndices.TransferIndex.HasValue)
        {
            score += 250;

            // Bonus if transfer queue is on a separate queue family from graphics
            // This allows streaming assets without blocking rendering
            if (specs.QueueFamilyIndices.TransferIndex.Value != specs.QueueFamilyIndices.GraphicsIndex)
            {
                score += 100;
            }
        }

        // Score based on present mode support
        // Mailbox mode (triple buffering) provides low latency with smooth frame pacing
        if (specs.PresentModes.Contains(PresentModeKHR.MailboxKhr))
        {
            score += 50;
        }

        // Immediate mode useful for benchmarking and uncapped frame rates
        if (specs.PresentModes.Contains(PresentModeKHR.ImmediateKhr))
        {
            score += 25;
        }

        // Score based on swapchain flexibility
        // Prefer devices that support more swapchain images for smoother frame pacing
        uint maxSwapchainImages = specs.SurfaceCapabilities.MaxImageCount;
        if (maxSwapchainImages == 0) // 0 means no limit
        {
            score += 30;
        }
        else if (maxSwapchainImages > 3)
        {
            score += 30;
        }

        // Prefer devices that support SRGB color space for proper gamma correction
        bool supportsSRGB = specs.SurfaceFormats.Any(f => f.Format == Format.B8G8R8A8Srgb && f.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr);
        if (supportsSRGB)
        {
            score += 20;
        }

        // Score based on available video memory (VRAM)
        // More VRAM = more textures, bigger scenes, better performance
        ulong totalVRAM = 0;
        for (int i = 0; i < specs.PhysicalDeviceMemoryProperties.MemoryHeapCount; i++)
        {
            MemoryHeap heap = specs.PhysicalDeviceMemoryProperties.MemoryHeaps[i];
            if (heap.Flags.HasFlag(MemoryHeapFlags.DeviceLocalBit))
            {
                totalVRAM += heap.Size;
            }
        }
        // Add 1 point per MB of VRAM
        score += (int)(totalVRAM / (1024 * 1024));

        // Score based on max texture dimensions (but cap the influence)
        // Modern GPUs all support large textures; this shouldn't dominate the score
        int textureDimensionScore = (int)specs.PhysicalDeviceProperties.Limits.MaxImageDimension2D / 1000;
        score += Math.Min(textureDimensionScore, 20); // Cap at 20 points

        return score;
    }

    private static Device CreateLogicalDevice(Vk vk, PhysicalDevice physicalDevice, QueueFamilyIndices indices, VulkanSurface surface, bool validationLayersEnabled)
    {
        List<uint> queueFamiliesList = [indices.GraphicsIndex, indices.PresentIndex];
        if (indices.ComputeIndex.HasValue)
        {
            queueFamiliesList.Add(indices.ComputeIndex.Value);
        }
        if (indices.TransferIndex.HasValue)
        {
            queueFamiliesList.Add(indices.TransferIndex.Value);
        }
        uint[] uniqueQueueFamilies = [.. queueFamiliesList.Distinct()];

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
        PhysicalDeviceFeatures deviceFeatures = new()
        {
            SamplerAnisotropy = true
        };

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

        if (validationLayersEnabled)
        {
            string[] validationLayers = VulkanDebugger.GetEnabledLayerNames();
            createInfo.EnabledLayerCount = (uint)validationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(validationLayers);
        }

        if (vk.CreateDevice(physicalDevice, in createInfo, null, out Device logicalDevice) != Result.Success)
        {
            throw new Exception("Failed to create logical device.");
        }

        if (validationLayersEnabled)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }

        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        return logicalDevice;
    }

    private static CommandPool CreateCommandPool(Vk vk, Device logicalDevice, uint queueFamilyIndex)
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            Flags = CommandPoolCreateFlags.ResetCommandBufferBit, // Allow individual command buffer resets
            QueueFamilyIndex = queueFamilyIndex
        };

        if (vk.CreateCommandPool(logicalDevice, in poolInfo, null, out CommandPool commandPool) != Result.Success)
        {
            throw new Exception($"Failed to create command pool for queue family {queueFamilyIndex}.");
        }

        return commandPool;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (TransferCommandPool.HasValue)
        {
            _vk.DestroyCommandPool(LogicalDevice, TransferCommandPool.Value, null);
        }

        if (ComputeCommandPool.HasValue)
        {
            _vk.DestroyCommandPool(LogicalDevice, ComputeCommandPool.Value, null);
        }

        _vk.DestroyCommandPool(LogicalDevice, GraphicsCommandPool, null);

        _vk.DestroyDevice(LogicalDevice, null);
    }
}

internal readonly struct QueueFamilyIndices
{
    public required uint GraphicsIndex { get; init; }
    public required uint PresentIndex { get; init; }
    public uint? ComputeIndex { get; init; }
    public uint? TransferIndex { get; init; }
}

internal record PhysicalDeviceSpecs
{
    internal QueueFamilyIndices QueueFamilyIndices { get; init; }
    internal PhysicalDeviceProperties PhysicalDeviceProperties { get; init; }
    internal PhysicalDeviceMemoryProperties PhysicalDeviceMemoryProperties { get; init; }
    internal SurfaceCapabilitiesKHR SurfaceCapabilities { get; init; }
    internal SurfaceFormatKHR[] SurfaceFormats { get; init; } = [];
    internal PresentModeKHR[] PresentModes { get; init; } = [];
}
