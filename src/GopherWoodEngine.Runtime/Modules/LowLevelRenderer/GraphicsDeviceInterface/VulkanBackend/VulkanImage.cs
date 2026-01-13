using Silk.NET.Vulkan;
using SixLabors.ImageSharp.PixelFormats;
using System;
using Image = Silk.NET.Vulkan.Image;
using SLImage = SixLabors.ImageSharp.Image;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

/// <summary>
/// Manages Vulkan image resources including image views and samplers for texture rendering.
/// </summary>
internal sealed class VulkanImage : IDisposable
{
    /// <summary>
    /// Gets the width of the image in pixels.
    /// </summary>
    internal uint Width { get; }

    /// <summary>
    /// Gets the height of the image in pixels.
    /// </summary>
    internal uint Height { get; }

    /// <summary>
    /// Gets the pixel format of the image.
    /// </summary>
    internal Format Format { get; }

    /// <summary>
    /// Gets the image view used for accessing the image in shaders and render passes.
    /// </summary>
    internal ImageView ImageView { get; }

    /// <summary>
    /// Gets the sampler used for sampling this image in shaders.
    /// </summary>
    /// <exception cref="NullReferenceException">
    /// Thrown when the sampler has not been initialized via <see cref="CreateSampler"/>.
    /// </exception>
    /// <remarks>
    /// The sampler must be explicitly created using <see cref="CreateSampler"/> before accessing this property.
    /// </remarks>
    internal Sampler Sampler { get => _sampler.Handle != 0 ? _sampler : throw new NullReferenceException("Sampler not initialised yet."); }

    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly ImageAspectFlags _aspectMask;
    private readonly Image _image;
    private readonly DeviceMemory _imageMemory;
    private Sampler _sampler;
    private ImageLayout _layout = ImageLayout.Undefined;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanImage"/> class from an image file.
    /// </summary>
    /// <param name="imageFile">The path to the image file to load.</param>
    /// <param name="format">The Vulkan format to use for the image.</param>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <param name="aspectMask">The aspect mask for the image. Default is <see cref="ImageAspectFlags.ColorBit"/>.</param>
    /// <remarks>
    /// <para>
    /// The image is created in device-local memory for optimal GPU access performance. The staging
    /// buffer is automatically disposed after the upload completes.
    /// </para>
    /// <para>
    /// Supported file formats include PNG, JPEG, BMP, GIF, TGA, and other formats supported by
    /// SixLabors.ImageSharp. The image data is converted to RGBA32 format during loading.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when image creation, memory allocation, or upload operations fail.
    /// </exception>
    public VulkanImage(string imageFile, Format format, Vk vk, VulkanDevices devices, ImageAspectFlags aspectMask = ImageAspectFlags.ColorBit)
    {
        _vk = vk;
        _devices = devices;
        _aspectMask = CompleteAspectMask(aspectMask, format);
        Format = format;
        (byte[] imgData, uint width, uint height) = LoadImage(imageFile);
        Width = width;
        Height = height;

        VulkanBuffer<byte> stagingBuffer = new(
            imgData,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            _vk,
            devices);

        _image = CreateImage(ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit, out _imageMemory);
        UploadToImage(stagingBuffer);
        TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        stagingBuffer.Dispose();
        ImageView = CreateImageView();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanImage"/> class with specific dimensions and usage.
    /// </summary>
    /// <param name="width">The width of the image in pixels.</param>
    /// <param name="height">The height of the image in pixels.</param>
    /// <param name="format">The pixel format of the image.</param>
    /// <param name="usage">The intended usage flags for the image.</param>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <param name="aspectMask">The aspect mask for the image. Default is <see cref="ImageAspectFlags.DepthBit"/>.</param>
    /// <remarks>
    /// <para>
    /// This constructor creates an image without initial data, suitable for use as render targets,
    /// depth buffers, or other GPU-generated content. The image is created in device-local memory.
    /// </para>
    /// <para>
    /// For depth/stencil formats, the aspect mask is automatically completed to include stencil
    /// components if the format supports them.
    /// </para>
    /// <para>
    /// The image layout starts as <see cref="ImageLayout.Undefined"/> and must be transitioned
    /// to an appropriate layout before use via <see cref="TransitionImageLayout"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when image creation, memory allocation, or image view creation fails.
    /// </exception>
    public VulkanImage(uint width, uint height, Format format, ImageUsageFlags usage, Vk vk, VulkanDevices devices, ImageAspectFlags aspectMask = ImageAspectFlags.DepthBit)
    {
        _vk = vk;
        _devices = devices;
        Width = width;
        Height = height;
        _aspectMask = CompleteAspectMask(aspectMask, format);
        Format = format;
        _image = CreateImage(usage, out _imageMemory);
        ImageView = CreateImageView();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanImage"/> class from an image file using SRGB format.
    /// </summary>
    /// <param name="imageFile">The path to the image file to load.</param>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <remarks>
    /// This is a convenience constructor that loads the image using <see cref="Format.R8G8B8A8Srgb"/> format,
    /// which is suitable for most color textures with proper gamma correction.
    /// </remarks>
    public VulkanImage(string imageFile, Vk vk, VulkanDevices devices) : this(imageFile, Format.R8G8B8A8Srgb, vk, devices) { }

    /// <summary>
    /// Creates a sampler for this image with the specified filtering mode.
    /// </summary>
    /// <param name="filter">The filtering mode to use for texture sampling (e.g., Linear, Nearest).</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when sampler creation fails.
    /// </exception>
    internal unsafe void CreateSampler(Filter filter)
    {
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = filter,
            MinFilter = filter,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = _devices.PhysicalDeviceSpecs.PhysicalDeviceProperties.Limits.MaxSamplerAnisotropy,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0.0f,
            MinLod = 0.0f,
            MaxLod = 0.0f
        };

        if (_vk.CreateSampler(_devices.LogicalDevice, in samplerInfo, null, out _sampler) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create image sampler.");
        }
    }

    /// <summary>
    /// Uploads data from a staging buffer to the GPU image.
    /// </summary>
    /// <param name="stagingBuffer">The staging buffer containing the image data to upload.</param>
    /// <remarks>
    /// This is a synchronous operation that blocks until the upload completes. For production code,
    /// consider batching multiple uploads or using asynchronous transfer queues for better performance.
    /// </remarks>
    internal unsafe void UploadToImage(VulkanBuffer<byte> stagingBuffer)
    {
        TransitionImageLayout(ImageLayout.TransferDstOptimal);

        CommandBuffer commandBuffer = _devices.BeginSingleUseCommandBuffer(_devices.GraphicsCommandPool);

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = _aspectMask,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            },
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D
            {
                Width = Width,
                Height = Height,
                Depth = 1
            }
        };

        _vk.CmdCopyBufferToImage(commandBuffer, stagingBuffer.Handle, _image, ImageLayout.TransferDstOptimal, 1, region);

        _devices.EndSingleUseCommandBuffer(commandBuffer, _devices.GraphicsQueue, _devices.GraphicsCommandPool);
    }

    /// <summary>
    /// Transitions the image from its current layout to a new layout using pipeline barriers.
    /// </summary>
    /// <param name="newLayout">The target image layout.</param>
    /// <remarks>
    /// <para>
    /// The method automatically configures appropriate pipeline stages and access masks for each
    /// transition to ensure proper synchronization. If the image is already in the target layout,
    /// the operation is skipped.
    /// </para>
    /// <para>
    /// This is a synchronous operation that blocks until the transition completes.
    /// </para>
    /// </remarks>
    internal unsafe void TransitionImageLayout(ImageLayout newLayout)
    {
        if (_layout == newLayout)
        {
            return;
        }

        CommandBuffer commandBuffer = _devices.BeginSingleUseCommandBuffer(_devices.GraphicsCommandPool);

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = _layout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = _image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = _aspectMask,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        PipelineStageFlags sourceStage = 0;
        PipelineStageFlags destinationStage = 0;

        if (_layout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (_layout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;

            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else if (_layout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.DepthStencilAttachmentReadBit | AccessFlags.DepthStencilAttachmentWriteBit;

            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.EarlyFragmentTestsBit;
        }

        _vk.CmdPipelineBarrier(commandBuffer, sourceStage, destinationStage, 0, 0, null, 0, null, 1, in barrier);

        _devices.EndSingleUseCommandBuffer(commandBuffer, _devices.GraphicsQueue, _devices.GraphicsCommandPool);
        _layout = newLayout;
    }

    /// <summary>
    /// Creates a descriptor image info structure for use in descriptor sets.
    /// </summary>
    /// <returns>A <see cref="DescriptorImageInfo"/> describing this image's current state.</returns>
    /// <remarks>
    /// This method is typically used when creating or updating descriptor sets that reference this
    /// image for shader access. The returned structure includes the current image layout, image view,
    /// and sampler.
    /// </remarks>
    /// <exception cref="NullReferenceException">
    /// Thrown when accessing the <see cref="Sampler"/> property if the sampler has not been created.
    /// </exception>
    internal DescriptorImageInfo ImageInfo()
    {
        return new DescriptorImageInfo()
        {
            ImageLayout = _layout,
            ImageView = ImageView,
            Sampler = Sampler
        };
    }

    private unsafe Image CreateImage(ImageUsageFlags usage, out DeviceMemory imageMemory)
    {
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            MipLevels = 1,
            ArrayLayers = 1,
            Format = Format,
            Tiling = ImageTiling.Optimal,
            InitialLayout = ImageLayout.Undefined,
            Usage = usage,
            SharingMode = SharingMode.Exclusive,
            Samples = SampleCountFlags.Count1Bit,
            Flags = 0,
            Extent = new Extent3D
            {
                Width = Width,
                Height = Height,
                Depth = 1
            }
        };

        if (_vk.CreateImage(_devices.LogicalDevice, in imageInfo, null, out Image image) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create image.");
        }

        MemoryRequirements memRequirements = _vk.GetImageMemoryRequirements(_devices.LogicalDevice, image);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = VulkanUtilities.FindMemoryType(
                memRequirements.MemoryTypeBits,
                MemoryPropertyFlags.DeviceLocalBit,
                _devices.PhysicalDeviceSpecs.PhysicalDeviceMemoryProperties)
        };

        if (_vk.AllocateMemory(_devices.LogicalDevice, in allocInfo, null, out imageMemory) != Result.Success)
        {
            throw new InvalidOperationException("Failed to allocate image memory.");
        }

        VulkanUtilities.AssertVk(_vk.BindImageMemory(_devices.LogicalDevice, image, imageMemory, 0));

        return image;
    }

    private unsafe ImageView CreateImageView()
    {
        ImageViewCreateInfo viewInfo = new()
        {
            SType = StructureType.ImageViewCreateInfo,
            Image = _image,
            ViewType = ImageViewType.Type2D,
            Format = Format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = _aspectMask,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        if (_vk.CreateImageView(_devices.LogicalDevice, in viewInfo, null, out ImageView imageView) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create ImageView!");
        }

        return imageView;
    }

    private static ImageAspectFlags CompleteAspectMask(ImageAspectFlags aspectMask, Format format)
    {
        bool hasStencilComponent = (format == Format.D32SfloatS8Uint) || (format == Format.D24UnormS8Uint);
        if (hasStencilComponent)
        {
            return aspectMask | ImageAspectFlags.StencilBit;
        }

        return aspectMask;
    }

    private static (byte[] imgData, uint width, uint height) LoadImage(string imageFile)
    {
        using SixLabors.ImageSharp.Image<Rgba32> image = SLImage.Load<Rgba32>(imageFile);
        uint width = (uint)image.Width;
        uint height = (uint)image.Height;

        byte[] pixels = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(pixels);

        return (pixels, width, height);
    }

    /// <inheritdoc/>
    public unsafe void Dispose()
    {
        if (_sampler.Handle != 0)
        {
            _vk.DestroySampler(_devices.LogicalDevice, Sampler, null);
        }

        _vk.DestroyImageView(_devices.LogicalDevice, ImageView, null);
        _vk.FreeMemory(_devices.LogicalDevice, _imageMemory, null);
        _vk.DestroyImage(_devices.LogicalDevice, _image, null);
    }
}
