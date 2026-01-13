using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

internal sealed class VulkanSwapChainNew : IDisposable
{
    public const int MAX_FRAMES_IN_FLIGHT = 2;

    public KhrPushDescriptor PushDescriptor { get; }
    public SurfaceFormatKHR SurfaceFormat { get; private set; }
    public Format ImageFormat => SurfaceFormat.Format;
    public PresentModeKHR PresentMode { get; private set; }
    public Extent2D Extent { get; private set; }
    public SwapchainKHR Handle { get; private set; }
    public RenderPass RenderPass { get; }
    public VulkanFrame[] Frames { get; }
    public int CurrentFrameIndex { get; private set; }

    private readonly IWindow _window;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private readonly KhrSwapchain _extSwapchain;

    private readonly VulkanSurface _surface;
    private readonly VulkanDevices _devices;
    private Image[] _swapchainImages;
    private ImageView[] _imageViews;
    private VulkanImage[] _depthImages;
    private Framebuffer[] _framebuffers;
    private uint _swapchainImageIndex;
    private bool _resized = false;

    public VulkanSwapChainNew(IWindow window, VulkanAPI vulkanAPI, VulkanSurface surface, VulkanDevices devices)
    {
        _window = window;
        _vk = vulkanAPI.Vk;
        _instance = vulkanAPI.Instance;
        _surface = surface;
        _devices = devices;
        SurfaceFormat = ChooseSwapSurfaceFormat();
        PresentMode = ChooseSwapPresentMode();
        Extent = ChooseSwapExtent();
        _extSwapchain = GetSwapchainExtension();
        Handle = CreateSwapchain(out _swapchainImages);
        _imageViews = CreateSwapchainImageViews();
        _depthImages = CreateDepthImages();

        RenderPass = CreateRenderPass();

        _framebuffers = CreateFramebuffers();

        CommandBuffer[] commandBuffers = AllocateCommandBuffers(MAX_FRAMES_IN_FLIGHT, _vk, devices);
        Frames = new VulkanFrame[MAX_FRAMES_IN_FLIGHT];
        for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
        {
            Frames[i] = new VulkanFrame(_vk, devices, this, commandBuffers[i]);
        }

        CurrentFrameIndex = -1;

        if (!_vk.TryGetDeviceExtension(vulkanAPI.Instance, devices.LogicalDevice, out KhrPushDescriptor khrPushDescriptor))
        {
            throw new NotSupportedException($"{KhrPushDescriptor.ExtensionName} extension not found.");
        }
        PushDescriptor = khrPushDescriptor;
    }

    internal void OnResize(object? sender, WindowResizeEventArgs e)
    {
        _resized = true;
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat()
    {
        foreach (SurfaceFormatKHR availableFormat in _devices.PhysicalDeviceSpecs.SurfaceFormats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.PaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return _devices.PhysicalDeviceSpecs.SurfaceFormats[0];
    }

    private PresentModeKHR ChooseSwapPresentMode()
    {
        foreach (PresentModeKHR availablePresentMode in _devices.PhysicalDeviceSpecs.PresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    private Extent2D ChooseSwapExtent()
    {
        if (_devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageExtent.Width != uint.MaxValue)
        {
            return _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageExtent;
        }
        else
        {
            Extent2D actualExtent = CreateFramebufferExtent();

            actualExtent.Width = Math.Max(
                _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MinImageExtent.Width,
                Math.Min(actualExtent.Width,
                _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageExtent.Width));

            actualExtent.Height = Math.Max(
                _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MinImageExtent.Height,
                Math.Min(actualExtent.Height,
                _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageExtent.Height));

            return actualExtent;
        }
    }

    private Extent2D CreateFramebufferExtent()
    {
        uint width = (uint)_window.FramebufferSize.X;
        uint height = (uint)_window.FramebufferSize.Y;

        return new(width, height);
    }

    private unsafe KhrSwapchain GetSwapchainExtension()
    {
        if (!_vk.TryGetDeviceExtension(_instance, _devices.LogicalDevice, out KhrSwapchain extSwapchain))
        {
            throw new InvalidOperationException($"{KhrSwapchain.ExtensionName} extension not found.");
        }

        return extSwapchain;
    }

    private unsafe SwapchainKHR CreateSwapchain(out Image[] swapchainImages)
    {
        uint imageCount = _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MinImageCount + 1;
        if (_devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageCount > 0 && imageCount > _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageCount)
        {
            imageCount = _devices.PhysicalDeviceSpecs.SurfaceCapabilities.MaxImageCount;
        }

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = _surface.SurfaceKHR,
            MinImageCount = imageCount,
            ImageFormat = SurfaceFormat.Format,
            ImageColorSpace = SurfaceFormat.ColorSpace,
            ImageExtent = Extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = _devices.PhysicalDeviceSpecs.SurfaceCapabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = PresentMode,
            Clipped = true
        };

        QueueFamilyIndices indices = _devices.PhysicalDeviceSpecs.QueueFamilyIndices;
        uint* relevantIndices = stackalloc uint[2] { indices.GraphicsIndex, indices.PresentIndex };

        if (indices.GraphicsIndex != indices.PresentIndex)
        {
            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = 2;
            createInfo.PQueueFamilyIndices = relevantIndices;
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
            createInfo.QueueFamilyIndexCount = 0;
            createInfo.PQueueFamilyIndices = null;
        }

        if (_extSwapchain.CreateSwapchain(_devices.LogicalDevice, in createInfo, null, out SwapchainKHR swapChain) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create swap-chain.");
        }

        //TODO: Why are we getting swap-chain images here?
        VulkanUtilities.AssertVk(_extSwapchain.GetSwapchainImages(_devices.LogicalDevice, swapChain, ref imageCount, null));
        swapchainImages = new Image[imageCount];
        VulkanUtilities.AssertVk(_extSwapchain.GetSwapchainImages(_devices.LogicalDevice, swapChain, ref imageCount, out swapchainImages[0]));

        return swapChain;
    }

    private unsafe ImageView[] CreateSwapchainImageViews()
    {
        ImageView[] swapchainImageViews = new ImageView[_swapchainImages.Length];

        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            ImageViewCreateInfo createInfo = new()
            {
                SType = StructureType.ImageViewCreateInfo,
                Image = _swapchainImages[i],
                ViewType = ImageViewType.Type2D,
                Format = ImageFormat,
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

            if (_vk.CreateImageView(_devices.LogicalDevice, in createInfo, null, out swapchainImageViews[i]) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create swap-chain image view.");
            }
        }

        return swapchainImageViews;
    }

    private Format FindDepthFormat()
    {
        Format[] candidates = [Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint];

        foreach (Format format in candidates)
        {
            FormatProperties props = _vk.GetPhysicalDeviceFormatProperties(_devices.PhysicalDevice, format);
            if ((ImageTiling.Optimal == ImageTiling.Linear) && props.LinearTilingFeatures.HasFlag(FormatFeatureFlags.DepthStencilAttachmentBit))
            {
                return format;
            }
            else if (props.OptimalTilingFeatures.HasFlag(FormatFeatureFlags.DepthStencilAttachmentBit))
            {
                return format;
            }
        }

        throw new InvalidOperationException("Failed to find any supported acceptable format.");
    }

    private unsafe VulkanImage[] CreateDepthImages()
    {
        Format depthFormat = FindDepthFormat();
        VulkanImage[] depthImages = new VulkanImage[_swapchainImages.Length];
        for (int i = 0; i < depthImages.Length; i++)
        {
            depthImages[i] = new VulkanImage(Extent.Width, Extent.Height, depthFormat, ImageUsageFlags.DepthStencilAttachmentBit, _vk, _devices);
            depthImages[i].TransitionImageLayout(ImageLayout.DepthStencilAttachmentOptimal);
        }

        return depthImages;
    }

    private unsafe RenderPass CreateRenderPass()
    {
        AttachmentDescription* attachments = stackalloc AttachmentDescription[2]
        {
            new AttachmentDescription()
            {
                Format = ImageFormat,
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.Store,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.PresentSrcKhr
            },
            new AttachmentDescription()
            {
                Format = FindDepthFormat(),
                Samples = SampleCountFlags.Count1Bit,
                LoadOp = AttachmentLoadOp.Clear,
                StoreOp = AttachmentStoreOp.DontCare,
                StencilLoadOp = AttachmentLoadOp.DontCare,
                StencilStoreOp = AttachmentStoreOp.DontCare,
                InitialLayout = ImageLayout.Undefined,
                FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
            }
        };

        AttachmentReference colourAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colourAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        RenderPassCreateInfo renderPassCreateInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 2,
            PAttachments = attachments,
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        if (_vk.CreateRenderPass(_devices.LogicalDevice, in renderPassCreateInfo, null, out RenderPass renderPass) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create render pass.");
        }

        return renderPass;
    }

    private unsafe Framebuffer[] CreateFramebuffers()
    {
        Framebuffer[] framebuffers = new Framebuffer[_swapchainImages.Length];
        ImageView* attachments = stackalloc ImageView[2];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            attachments[0] = _imageViews[i];
            attachments[1] = _depthImages[i].ImageView;

            FramebufferCreateInfo framebufferCreateInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = RenderPass,
                AttachmentCount = 2,
                PAttachments = attachments,
                Width = Extent.Width,
                Height = Extent.Height,
                Layers = 1
            };

            if (_vk.CreateFramebuffer(_devices.LogicalDevice, in framebufferCreateInfo, null, out framebuffers[i]) != Result.Success)
            {
                throw new InvalidOperationException("Failed to create framebuffer.");
            }
        }

        return framebuffers;
    }

    private static unsafe CommandBuffer[] AllocateCommandBuffers(uint count, Vk vk, VulkanDevices devices)
    {
        CommandBuffer[] commandBuffers = new CommandBuffer[count];

        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = devices.GraphicsCommandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = count
        };

        if (vk.AllocateCommandBuffers(devices.LogicalDevice, in allocateInfo, out commandBuffers[0]) != Result.Success)
        {
            throw new InvalidOperationException("Failed to allocate CommandBuffer(s)!");
        }

        return commandBuffers;
    }

    private unsafe void CleanUpSwapchain()
    {
        foreach (Framebuffer framebuffer in _framebuffers)
        {
            _vk.DestroyFramebuffer(_devices.LogicalDevice, framebuffer, null);
        }

        foreach (VulkanImage depthImage in _depthImages)
        {
            depthImage.Dispose();
        }

        foreach (ImageView imageView in _imageViews)
        {
            _vk.DestroyImageView(_devices.LogicalDevice, imageView, null);
        }

        _extSwapchain.DestroySwapchain(_devices.LogicalDevice, Handle, null);
    }

    private unsafe void RecreateSwapchain()
    {
        VulkanUtilities.AssertVk(_vk.DeviceWaitIdle(_devices.LogicalDevice));

        SurfaceFormat = ChooseSwapSurfaceFormat();
        PresentMode = ChooseSwapPresentMode();
        Extent = ChooseSwapExtent();

        if (Extent.Width == 0 || Extent.Height == 0)
        {
            return;
        }

        CleanUpSwapchain();

        Handle = CreateSwapchain(out _swapchainImages);
        _imageViews = CreateSwapchainImageViews();
        _depthImages = CreateDepthImages();
        _framebuffers = CreateFramebuffers();
    }

    public VulkanFrame GetNextFrame()
    {
        CurrentFrameIndex = (CurrentFrameIndex + 1) % MAX_FRAMES_IN_FLIGHT;
        return Frames[CurrentFrameIndex];
    }

    public unsafe void BeginRenderPass(CommandBuffer cmd)
    {
        ClearValue* clearValues = stackalloc ClearValue[2]
        {
            new ClearValue(color: new ClearColorValue(0.1f, 0.5f, 1.0f, 1.0f)),
            new ClearValue(depthStencil: new ClearDepthStencilValue(1.0f, 0))
        };

        var renderPassBeginInfo = new RenderPassBeginInfo(
            renderPass: RenderPass,
            framebuffer: _framebuffers[_swapchainImageIndex],

            renderArea: new Rect2D(
                offset: new Offset2D(0, 0),
                extent: Extent
            ),

            clearValueCount: 2,
            pClearValues: clearValues
        );
        _vk.CmdBeginRenderPass(cmd, renderPassBeginInfo, SubpassContents.Inline);

        var viewport = new Viewport(
            x: 0.0f,
            y: 0.0f,
            width: Extent.Width,
            height: Extent.Height,
            minDepth: 0.0f,
            maxDepth: 1.0f
        );
        _vk.CmdSetViewport(cmd, 0, 1, viewport);

        var scissor = new Rect2D(
            offset: new Offset2D(0, 0),
            extent: Extent
        );
        _vk.CmdSetScissor(cmd, 0, 1, scissor);
    }

    public void EndRenderPass(CommandBuffer cmd)
    {
        _vk.CmdEndRenderPass(cmd);
    }

    public unsafe bool AcquireNextImage(Semaphore imageAvailableSemaphore)
    {
        Result result = _extSwapchain.AcquireNextImage(_devices.LogicalDevice, Handle, ulong.MaxValue, imageAvailableSemaphore, new Fence(handle: null), ref _swapchainImageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            RecreateSwapchain();
            return false;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new InvalidOperationException("Failed to acquire next swap-chain image.");
        }

        return true;
    }

    public unsafe void PresentImage(Semaphore waitSemaphore)
    {
        SwapchainKHR* swapchains = stackalloc SwapchainKHR[1] { Handle };
        uint* imageIndices = stackalloc uint[] { _swapchainImageIndex };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = &waitSemaphore,
            SwapchainCount = 1,
            PSwapchains = swapchains,
            PImageIndices = imageIndices,
            PResults = null
        };

        Result result = _extSwapchain.QueuePresent(_devices.PresentQueue, in presentInfo);
        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || _resized)
        {
            _resized = false;
            RecreateSwapchain();
        }
        else if (result != Result.Success)
        {
            throw new InvalidOperationException("Failed to present swap-chain image.");
        }
    }

    public unsafe void Dispose()
    {
        foreach (VulkanFrame frame in Frames)
        {
            frame.Dispose();
        }

        CleanUpSwapchain();

        _vk.DestroyRenderPass(_devices.LogicalDevice, RenderPass, null);
        _extSwapchain.Dispose();
    }
}
