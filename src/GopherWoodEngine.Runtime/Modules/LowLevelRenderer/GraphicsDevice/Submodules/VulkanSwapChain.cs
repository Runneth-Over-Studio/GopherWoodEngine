using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanSwapChain : IDisposable
{
    // Basic purpose is to ensure that the image that we're currently rendering to is different from the one that is currently on the screen. 
    // This is important to make sure that only complete images are shown.
    internal SwapchainKHR SwapChain { get; }

    // Resolution of the swap chain images. Almost always exactly equal to the resolution of the window that we're drawing to in pixels.
    internal Extent2D Extent { get; }

    // Specifies the color channels and types.
    internal Format ImageFormat { get; }

    // Image view for every image in the swap chain.
    // Image views describe how to access the image and which part of the image to access.
    internal ImageView[] ImageViews { get; }

    internal Image[] Images { get; }

    internal KhrSwapchain KhrSwapChain { get; }

    private readonly Vk _vk;
    private readonly Device _logicalDevice;
    
    private bool _disposed = false;

    public VulkanSwapChain(Vk vk, Instance instance, VulkanSurface surface, VulkanDevices devices, Vector2D<int> framebufferSize)
    {
        _vk = vk;
        _logicalDevice = devices.LogicalDevice;

        if (!vk.TryGetDeviceExtension(instance, _logicalDevice, out KhrSwapchain khrSwapChain))
        {
            throw new NotSupportedException("VK_KHR_swapchain extension not found.");
        }
        KhrSwapChain = khrSwapChain;

        SwapChainSupport swapChainSupport = surface.GetSwapChainSupport(devices.PhysicalDevice);
        Extent = ChooseSwapExtent(swapChainSupport.Capabilities, framebufferSize);

        uint imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
        if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
        {
            imageCount = swapChainSupport.Capabilities.MaxImageCount;
        }

        SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
        SwapChain = CreateSwapchain(vk, surface, devices, swapChainSupport, surfaceFormat, imageCount);

        KhrSwapChain.GetSwapchainImages(_logicalDevice, SwapChain, ref imageCount, null);
        Images = new Image[imageCount];
        fixed (Image* swapChainImagesPtr = Images)
        {
            KhrSwapChain.GetSwapchainImages(_logicalDevice, SwapChain, ref imageCount, swapChainImagesPtr);
        }

        ImageFormat = surfaceFormat.Format;
        ImageViews = CreateImageViews(vk, _logicalDevice, Images, ImageFormat);
    }

    private SwapchainKHR CreateSwapchain(Vk vk, VulkanSurface surface, VulkanDevices devices, SwapChainSupport swapChainSupport, SurfaceFormatKHR surfaceFormat, uint imageCount)
    {
        PresentModeKHR presentMode = ChoosePresentMode(swapChainSupport.PresentModes);

        SwapchainCreateInfoKHR creatInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface.SurfaceKHR,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = Extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };

        QueueFamilyIndices indices = devices.QueueFamilyIndices;
        uint* queueFamilyIndices = stackalloc[] { indices.GraphicsIndex, indices.PresentIndex };

        if (indices.GraphicsIndex != indices.PresentIndex)
        {
            creatInfo = creatInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices,
            };
        }
        else
        {
            creatInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        creatInfo = creatInfo with
        {
            PreTransform = swapChainSupport.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true,
            OldSwapchain = default
        };

        if (KhrSwapChain.CreateSwapchain(devices.LogicalDevice, in creatInfo, null, out SwapchainKHR swapChain) != Result.Success)
        {
            throw new Exception("Failed to create swap chain.");
        }

        return swapChain;
    }

    private static SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        foreach (SurfaceFormatKHR availableFormat in availableFormats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return availableFormats[0];
    }

    private static PresentModeKHR ChoosePresentMode(IReadOnlyList<PresentModeKHR> availablePresentModes)
    {
        foreach (PresentModeKHR availablePresentMode in availablePresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    private static Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities, Vector2D<int> framebufferSize)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }
        else
        {
            Extent2D actualExtent = new()
            {
                Width = (uint)framebufferSize.X,
                Height = (uint)framebufferSize.Y
            };

            actualExtent.Width = Math.Clamp(actualExtent.Width, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width);
            actualExtent.Height = Math.Clamp(actualExtent.Height, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height);

            return actualExtent;
        }
    }

    private static ImageView[] CreateImageViews(Vk vk, Device logicalDevice, Image[] swapChainImages, Format swapChainImageFormat)
    {
        ImageView[] swapChainImageViews = new ImageView[swapChainImages.Length];

        for (int i = 0; i < swapChainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = swapChainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = swapChainImageFormat,
                Components =
                {
                    R = ComponentSwizzle.Identity,
                    G = ComponentSwizzle.Identity,
                    B = ComponentSwizzle.Identity,
                    A = ComponentSwizzle.Identity
                },
                SubresourceRange =
                {
                    AspectMask = ImageAspectFlags.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = 1,
                    BaseArrayLayer = 0,
                    LayerCount = 1
                }
            };

            if (vk.CreateImageView(logicalDevice, in createInfo, null, out swapChainImageViews[i]) != Result.Success)
            {
                throw new Exception("Failed to create image views.");
            }
        }

        return swapChainImageViews;
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
                foreach (ImageView imageView in ImageViews)
                {
                    _vk.DestroyImageView(_logicalDevice, imageView, null);
                }

                KhrSwapChain.DestroySwapchain(_logicalDevice, SwapChain, null);
            }

            _disposed = true;
        }
    }
}
