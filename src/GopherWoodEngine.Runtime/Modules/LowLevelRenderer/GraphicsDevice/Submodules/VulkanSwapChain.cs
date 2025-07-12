using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;
using System.Collections.Generic;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanSwapChain : IDisposable
{
    internal KhrSwapchain KhrSwapChain { get; }

    // Resolution of the swap chain images. Almost always exactly equal to the resolution of the window that we're drawing to in pixels.
    internal Extent2D Extent { get; }

    // Basic purpose is to ensure that the image that we're currently rendering to is different from the one that is currently on the screen. 
    // This is important to make sure that only complete images are shown.
    internal SwapchainKHR SwapChain { get; private set; }

    internal Image[] Images { get; private set; }

    // Specifies the color channels and types.
    internal Format ImageFormat { get; }

    // Image view for every image in the swap chain.
    // Image views describe how to access the image and which part of the image to access.
    internal ImageView[] ImageViews { get; private set; }

    private readonly Vk _vk;
    private readonly VulkanSurface _surface;
    private readonly VulkanDevices _devices;
    private readonly SwapChainSupport _swapChainSupport;
    private readonly SurfaceFormatKHR _surfaceFormat;
    private bool _disposed = false;

    public VulkanSwapChain(Vk vk, Instance instance, VulkanSurface surface, VulkanDevices devices, Vector2D<int> framebufferSize)
    {
        _vk = vk;
        _surface = surface;
        _devices = devices;

        if (!vk.TryGetDeviceExtension(instance, devices.LogicalDevice, out KhrSwapchain khrSwapChain))
        {
            throw new NotSupportedException("VK_KHR_swapchain extension not found.");
        }
        KhrSwapChain = khrSwapChain;

        _swapChainSupport = surface.GetSwapChainSupport(devices.PhysicalDevice);
        Extent = ChooseSwapExtent(_swapChainSupport.Capabilities, framebufferSize);

        uint imageCount = _swapChainSupport.Capabilities.MinImageCount + 1;
        if (_swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > _swapChainSupport.Capabilities.MaxImageCount)
        {
            imageCount = _swapChainSupport.Capabilities.MaxImageCount;
        }

        _surfaceFormat = ChooseSwapSurfaceFormat(_swapChainSupport.Formats);
        SwapChain = CreateSwapchain(vk, surface, devices, KhrSwapChain, _swapChainSupport, Extent, _surfaceFormat, imageCount);
        Images = CreateImages(_devices.LogicalDevice, ref imageCount, KhrSwapChain, SwapChain);
        ImageFormat = _surfaceFormat.Format;
        ImageViews = CreateImageViews(vk, devices.LogicalDevice, Images, ImageFormat);
    }

    internal void CleanUpSwapChain()
    {
        foreach (ImageView imageView in ImageViews)
        {
            _vk.DestroyImageView(_devices.LogicalDevice, imageView, null);
        }

        KhrSwapChain.DestroySwapchain(_devices.LogicalDevice, SwapChain, null);
    }

    internal void ResetSwapChain()
    {
        uint imageCount = _swapChainSupport.Capabilities.MinImageCount + 1;
        if (_swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > _swapChainSupport.Capabilities.MaxImageCount)
        {
            imageCount = _swapChainSupport.Capabilities.MaxImageCount;
        }

        SwapChain = CreateSwapchain(_vk, _surface, _devices, KhrSwapChain, _swapChainSupport, Extent, _surfaceFormat, imageCount);
        Images = CreateImages(_devices.LogicalDevice, ref imageCount, KhrSwapChain, SwapChain);
        ImageViews = CreateImageViews(_vk, _devices.LogicalDevice, Images, ImageFormat);
    }

    private static SwapchainKHR CreateSwapchain(Vk vk, VulkanSurface surface, VulkanDevices devices, KhrSwapchain khrSwapChain, SwapChainSupport swapChainSupport, Extent2D extent, SurfaceFormatKHR surfaceFormat, uint imageCount)
    {
        PresentModeKHR presentMode = ChoosePresentMode(swapChainSupport.PresentModes);

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = surface.SurfaceKHR,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit
        };

        QueueFamilyIndices indices = devices.QueueFamilyIndices;
        uint* queueFamilyIndices = stackalloc[] { indices.GraphicsIndex, indices.PresentIndex };

        if (indices.GraphicsIndex != indices.PresentIndex)
        {
            createInfo = createInfo with
            {
                ImageSharingMode = SharingMode.Concurrent,
                QueueFamilyIndexCount = 2,
                PQueueFamilyIndices = queueFamilyIndices,
            };
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        createInfo = createInfo with
        {
            PreTransform = swapChainSupport.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true
        };

        if (khrSwapChain.CreateSwapchain(devices.LogicalDevice, in createInfo, null, out SwapchainKHR swapChain) != Result.Success)
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

    private static Image[] CreateImages(Device logicalDevice, ref uint imageCount, KhrSwapchain khrSwapChain, SwapchainKHR swapChain)
    {
        Image[] images = new Image[imageCount];

        khrSwapChain.GetSwapchainImages(logicalDevice, swapChain, ref imageCount, null);

        fixed (Image* swapChainImagesPtr = images)
        {
            khrSwapChain.GetSwapchainImages(logicalDevice, swapChain, ref imageCount, swapChainImagesPtr);
        }

        return images;
    }

    private static ImageView[] CreateImageViews(Vk vk, Device logicalDevice, Image[] swapChainImages, Format swapChainImageFormat)
    {
        ImageView[] swapChainImageViews = new ImageView[swapChainImages.Length];

        for (int i = 0; i < swapChainImages.Length; i++)
        {
            swapChainImageViews[i] = VulkanUtilities.CreateImageView(vk, logicalDevice, swapChainImages[i], swapChainImageFormat);
        }

        return swapChainImageViews;
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
                foreach (ImageView imageView in ImageViews)
                {
                    _vk.DestroyImageView(_devices.LogicalDevice, imageView, null);
                }

                KhrSwapChain.DestroySwapchain(_devices.LogicalDevice, SwapChain, null);
            }

            _disposed = true;
        }
    }
}
