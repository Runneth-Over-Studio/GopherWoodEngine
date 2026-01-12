using Silk.NET.Core;
using Silk.NET.Core.Contexts;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

internal unsafe sealed class VulkanSurface : IDisposable
{
    // Represents an abstract type of surface to present rendered images to.
    internal SurfaceKHR SurfaceKHR { get; }

    internal KhrSurface KhrSurface { get; }

    private readonly Instance _instance;
    private bool _isDisposed = false;

    public VulkanSurface(IVkSurface vkSurface, VulkanAPI vulkanAPI)
    {
        _instance = vulkanAPI.Instance;
        KhrSurface = CreateSurfaceExtension(vulkanAPI);
        SurfaceKHR = vkSurface.Create<AllocationCallbacks>(vulkanAPI.Instance.ToHandle(), null).ToSurface();
    }

    // Determine whether queue family has the capability of presenting to our window surface.
    internal bool PresentIsSupported(PhysicalDevice physicalDevice, uint queueFamilyIndex)
    {
        KhrSurface.GetPhysicalDeviceSurfaceSupport(physicalDevice, queueFamilyIndex, SurfaceKHR, out Bool32 presentSupport);

        return presentSupport;
    }

    // Return basic surface capabilities (min/max number of images in swap chain, min/max width and height of images),
    // surface formats (pixel format, color space), and available presentation modes.
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

    private static KhrSurface CreateSurfaceExtension(VulkanAPI vulkanAPI)
    {
        if (!vulkanAPI.Vk.TryGetInstanceExtension(vulkanAPI.Instance, out KhrSurface khrSurface))
        {
            throw new NotSupportedException($"{KhrSurface.ExtensionName} extension not found.");
        }

        return khrSurface;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                KhrSurface.DestroySurface(_instance, SurfaceKHR, null);
            }

            _isDisposed = true;
        }
    }
}

internal struct SwapChainSupport
{
    public SurfaceCapabilitiesKHR Capabilities;
    public SurfaceFormatKHR[] Formats;
    public PresentModeKHR[] PresentModes;
}
