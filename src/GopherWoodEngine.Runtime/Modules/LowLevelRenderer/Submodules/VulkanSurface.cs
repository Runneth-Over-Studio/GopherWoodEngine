using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.Submodules;

internal unsafe class VulkanSurface : IDisposable
{
    /// <summary>
    /// Instance level extension that exposes a VkSurfaceKHR and also includes some other WSI extensions.
    /// </summary>
    internal KhrSurface KhrSurface { get; }

    /// <summary>
    /// Represents an abstract type of surface to present rendered images to.
    /// </summary>
    internal SurfaceKHR SurfaceKHR { get; }

    private readonly Instance _instance;
    private bool _disposed = false;

    public VulkanSurface(IWindow window, Instance instance, Vk vk)
    {
        _instance = instance;

        KhrSurface = CreateSurfaceExtension(vk, instance);
        SurfaceKHR = CreateAbstractSurface(window, instance);
    }

    /// <summary>
    /// Determine whether queue family has the capability of presenting to our window surface.
    /// </summary>
    internal bool PresentIsSupported(PhysicalDevice physicalDevice, uint queueFamilyIndex)
    {
        KhrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, queueFamilyIndex, SurfaceKHR, out Bool32 presentSupport);

        return presentSupport;
    }

    /// <summary>
    /// Return basic surface capabilities (min/max number of images in swap chain, min/max width and height of images),
    /// surface formats (pixel format, color space), and available presentation modes.
    /// </summary>
    internal SwapChainSupport GetSwapChainSupport(PhysicalDevice physicalDevice)
    {
        // Basic surface capabilities.
        KhrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, SurfaceKHR, out SurfaceCapabilitiesKHR capabilities);

        // Surface formats.
        uint formatCount = 0;
        KhrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, SurfaceKHR, ref formatCount, null);

        SurfaceFormatKHR[] formats;
        if (formatCount != 0)
        {
            formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = formats)
            {
                KhrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, SurfaceKHR, ref formatCount, formatsPtr);
            }
        }
        else
        {
            formats = [];
        }

        // Available presentation modes.
        uint presentModeCount = 0;
        KhrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, SurfaceKHR, ref presentModeCount, null);

        PresentModeKHR[] presentModes;
        if (presentModeCount != 0)
        {
            presentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* formatsPtr = presentModes)
            {
                KhrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, SurfaceKHR, ref presentModeCount, formatsPtr);
            }

        }
        else
        {
            presentModes = [];
        }

        return new SwapChainSupport()
        {
            Capabilities = capabilities,
            Formats = formats,
            PresentModes = presentModes
        };
    }

    private static KhrSurface CreateSurfaceExtension(Vk vk, Instance instance)
    {
        if (!vk.TryGetInstanceExtension(instance, out KhrSurface khrSurface))
        {
            throw new NotSupportedException("KHR_surface extension not found.");
        }

        return khrSurface;
    }

    private static SurfaceKHR CreateAbstractSurface(IWindow window, Instance instance)
    {
        SurfaceKHR surfaceKHR = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();

        return surfaceKHR;
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
                KhrSurface!.DestroySurface(_instance, SurfaceKHR, null);
            }

            _disposed = true;
        }
    }
}

internal struct SwapChainSupport
{
    public SurfaceCapabilitiesKHR Capabilities;
    public SurfaceFormatKHR[] Formats;
    public PresentModeKHR[] PresentModes;
}
