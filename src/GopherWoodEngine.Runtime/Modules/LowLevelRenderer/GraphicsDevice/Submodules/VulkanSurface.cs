using Silk.NET.Core;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanSurface : IDisposable
{
    /// <summary>
    /// Represents an abstract type of surface to present rendered images to.
    /// </summary>
    internal SurfaceKHR SurfaceKHR { get; }

    private readonly KhrSurface _khrSurface;
    private readonly Instance _instance;
    private bool _disposed = false;

    public VulkanSurface(IWindow window, Instance instance, Vk vk)
    {
        _instance = instance;

        _khrSurface = CreateSurfaceExtension(vk, instance);
        SurfaceKHR = CreateAbstractSurface(window, instance);
    }

    /// <summary>
    /// Determine whether queue family has the capability of presenting to our window surface.
    /// </summary>
    internal bool PresentIsSupported(PhysicalDevice physicalDevice, uint queueFamilyIndex)
    {
        _khrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, queueFamilyIndex, SurfaceKHR, out Bool32 presentSupport);

        return presentSupport;
    }

    /// <summary>
    /// Return basic surface capabilities (min/max number of images in swap chain, min/max width and height of images),
    /// surface formats (pixel format, color space), and available presentation modes.
    /// </summary>
    internal SwapChainSupport GetSwapChainSupport(PhysicalDevice physicalDevice)
    {
        // Basic surface capabilities.
        _khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, SurfaceKHR, out SurfaceCapabilitiesKHR capabilities);

        // Surface formats.
        uint formatCount = 0;
        _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, SurfaceKHR, ref formatCount, null);

        SurfaceFormatKHR[] formats;
        if (formatCount != 0)
        {
            formats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formatsPtr = formats)
            {
                _khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, SurfaceKHR, ref formatCount, formatsPtr);
            }
        }
        else
        {
            formats = [];
        }

        // Available presentation modes.
        uint presentModeCount = 0;
        _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, SurfaceKHR, ref presentModeCount, null);

        PresentModeKHR[] presentModes;
        if (presentModeCount != 0)
        {
            presentModes = new PresentModeKHR[presentModeCount];
            fixed (PresentModeKHR* formatsPtr = presentModes)
            {
                _khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, SurfaceKHR, ref presentModeCount, formatsPtr);
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
                _khrSurface!.DestroySurface(_instance, SurfaceKHR, null);
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
