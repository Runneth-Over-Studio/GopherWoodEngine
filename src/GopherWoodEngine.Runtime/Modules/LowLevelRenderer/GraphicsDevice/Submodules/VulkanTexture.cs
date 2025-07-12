using Silk.NET.Vulkan;
using System;
using Buffer = Silk.NET.Vulkan.Buffer;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanTexture : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _logicalDevice;
    private Image _textureImage;
    private DeviceMemory _textureImageMemory;
    private ImageView _textureImageView;
    private Sampler _textureSampler;
    private bool _disposed = false;

    public VulkanTexture(Vk vk, VulkanDevices devices, CommandPool commandPool)
    {
        _vk = vk;
        _logicalDevice = devices.LogicalDevice;
        (_textureImage, _textureImageMemory) = CreateImageWithMemory(vk, devices, commandPool, "textures/texture.jpg");
        _textureImageView = VulkanUtilities.CreateImageView(vk, devices.LogicalDevice, _textureImage, Format.R8G8B8A8Srgb);
        _textureSampler = CreateTextureSampler(vk, devices);
    }

    private static (Image, DeviceMemory) CreateImageWithMemory(Vk vk, VulkanDevices devices, CommandPool commandPool, string imagePath)
    {
        using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imagePath);

        ulong imageSize = (ulong)(img.Width * img.Height * img.PixelType.BitsPerPixel / 8);

        Buffer stagingBuffer = VulkanUtilities.CreateBuffer(vk, devices.LogicalDevice, imageSize, BufferUsageFlags.TransferSrcBit);
        DeviceMemory stagingBufferMemory = VulkanUtilities.CreateMemory(vk, devices, stagingBuffer, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data;
        vk.MapMemory(devices.LogicalDevice, stagingBufferMemory, 0, imageSize, 0, &data);
        img.CopyPixelDataTo(new Span<byte>(data, (int)imageSize));
        vk.UnmapMemory(devices.LogicalDevice, stagingBufferMemory);

        Image textureImage = CreateImage(vk, devices.LogicalDevice, (uint)img.Width, (uint)img.Height, Format.R8G8B8A8Srgb, ImageTiling.Optimal, ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit);
        DeviceMemory imageMemory = VulkanUtilities.CreateMemory(vk, devices, textureImage, MemoryPropertyFlags.DeviceLocalBit);

        TransitionImageLayout(vk, devices, commandPool, textureImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
        CopyBufferToImage(vk, devices, commandPool, stagingBuffer, textureImage, (uint)img.Width, (uint)img.Height);
        TransitionImageLayout(vk, devices, commandPool, textureImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

        vk.DestroyBuffer(devices.LogicalDevice, stagingBuffer, null);
        vk.FreeMemory(devices.LogicalDevice, stagingBufferMemory, null);

        return (textureImage, imageMemory);
    }

    private static Sampler CreateTextureSampler(Vk vk, VulkanDevices devices)
    {
        vk.GetPhysicalDeviceProperties(devices.PhysicalDevice, out PhysicalDeviceProperties properties);

        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear
        };

        if (vk.CreateSampler(devices.LogicalDevice, in samplerInfo, null, out Sampler textureSamplerPtr) != Result.Success)
        {
            throw new Exception("Failed to create logical device.");
        }

        return textureSamplerPtr;
    }

    private static Image CreateImage(Vk vk, Device logicalDevice, uint width, uint height, Format format, ImageTiling tiling, ImageUsageFlags usage)
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = format,
            Tiling = tiling,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            Samples = SampleCountFlags.Count1Bit,
            SharingMode = SharingMode.Exclusive,
            Extent =
            {
                Width = width,
                Height = height,
                Depth = 1
            }
        };

        if (vk.CreateImage(logicalDevice, in imageInfo, null, out Image image) != Result.Success)
        {
            throw new Exception("Failed to create image.");
        }

        return image;
    }

    private static void TransitionImageLayout(Vk vk, VulkanDevices devices, CommandPool commandPool, Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        CommandBuffer commandBuffer = VulkanUtilities.BeginSingleTimeCommands(vk, devices, commandPool);

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        PipelineStageFlags sourceStage;
        PipelineStageFlags destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new Exception("Unsupported layout transition.");
        }

        vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, in barrier);

        VulkanUtilities.EndSingleTimeCommands(vk, devices, commandPool, commandBuffer);
    }

    private static void CopyBufferToImage(Vk vk, VulkanDevices devices, CommandPool commandPool, Buffer buffer, Image image, uint width, uint height)
    {
        CommandBuffer commandBuffer = VulkanUtilities.BeginSingleTimeCommands(vk, devices, commandPool);

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
            ImageSubresource =
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        vk!.CmdCopyBufferToImage(commandBuffer, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);

        VulkanUtilities.EndSingleTimeCommands(vk, devices, commandPool, commandBuffer);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _vk.DestroySampler(_logicalDevice, _textureSampler, null);
                _vk.DestroyImageView(_logicalDevice, _textureImageView, null);

                _vk.DestroyImage(_logicalDevice, _textureImage, null);
                _vk.FreeMemory(_logicalDevice, _textureImageMemory, null);
            }

            _disposed = true;
        }
    }
}
