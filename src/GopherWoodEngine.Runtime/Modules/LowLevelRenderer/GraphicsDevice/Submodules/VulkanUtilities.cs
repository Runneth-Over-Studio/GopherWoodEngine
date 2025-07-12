using Silk.NET.Vulkan;
using System;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal static unsafe class VulkanUtilities
{
    internal static Buffer CreateBuffer(Vk vk, Device logicalDevice, ulong bufferSize, BufferUsageFlags usage)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufferSize,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(logicalDevice, in bufferInfo, null, out Buffer buffer) != Result.Success)
        {
            throw new Exception("Failed to create vertex buffer.");
        }

        return buffer;
    }

    internal static ImageView CreateImageView(Vk vk, Device logicalDevice, Image image, Format imageFormat)
    {
        ImageViewCreateInfo createInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = image,
            ViewType = ImageViewType.Type2D,
            Format = imageFormat,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (vk.CreateImageView(logicalDevice, in createInfo, null, out ImageView imageView) != Result.Success)
        {
            throw new Exception("Failed to create image views.");
        }

        return imageView;
    }

    internal static CommandBuffer BeginSingleTimeCommands(Vk vk, VulkanDevices devices, CommandPool commandPool)
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = commandPool,
            CommandBufferCount = 1
        };

        vk.AllocateCommandBuffers(devices.LogicalDevice, in allocateInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        return commandBuffer;
    }

    internal static void EndSingleTimeCommands(Vk vk, VulkanDevices devices, CommandPool commandPool, CommandBuffer commandBuffer)
    {
        vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        vk.QueueSubmit(devices.GraphicsQueue, 1, in submitInfo, default);
        vk.QueueWaitIdle(devices.GraphicsQueue);

        vk.FreeCommandBuffers(devices.LogicalDevice, commandPool, 1, in commandBuffer);
    }

    internal static DeviceMemory CreateMemory(Vk vk, VulkanDevices devices, Buffer buffer, MemoryPropertyFlags properties)
    {
        vk.GetBufferMemoryRequirements(devices.LogicalDevice, buffer, out MemoryRequirements memRequirements);

        DeviceMemory deviceMemory = CreateMemory(vk, devices, memRequirements, properties);

        vk.BindBufferMemory(devices.LogicalDevice, buffer, deviceMemory, 0);

        return deviceMemory;
    }

    internal static DeviceMemory CreateMemory(Vk vk, VulkanDevices devices, Image image, MemoryPropertyFlags properties)
    {
        vk.GetImageMemoryRequirements(devices.LogicalDevice, image, out MemoryRequirements memRequirements);

        DeviceMemory deviceMemory = CreateMemory(vk, devices, memRequirements, properties);

        vk.BindImageMemory(devices.LogicalDevice, image, deviceMemory, 0);

        return deviceMemory;
    }

    private static DeviceMemory CreateMemory(Vk vk, VulkanDevices devices, MemoryRequirements memRequirements, MemoryPropertyFlags properties)
    {
        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(vk, devices.PhysicalDevice, memRequirements.MemoryTypeBits, properties)
        };

        if (vk.AllocateMemory(devices.LogicalDevice, in allocateInfo, null, out DeviceMemory deviceMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate device memory.");
        }

        return deviceMemory;
    }

    private static uint FindMemoryType(Vk vk, PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new Exception("Failed to find suitable memory type.");
    }
}
