using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanPresenter : IDisposable
{
    private const int MAX_FRAMES_IN_FLIGHT = 2;

    internal VulkanDevices Devices { get { return _devices; } }

    private readonly VulkanSurface _surface;
    private readonly VulkanDevices _devices;
    private readonly VulkanSwapChain _swapChain;
    private readonly VulkanPipeline _pipeline;
    private readonly Framebuffer[] _framebuffers;
    private readonly CommandPool _commandPool;
    private readonly CommandBuffer[] _commandBuffers;
    private readonly Semaphore[] _imageAvailableSemaphores;
    private readonly Semaphore[] _renderFinishedSemaphores;
    private readonly Fence[] _inFlightFences;
    private readonly Fence[] _imagesInFlight;
    private readonly Vk _vk;
    private int _currentFrame = 0;
    private bool _disposed = false;

    public VulkanPresenter(IWindow window, Vk vk, Instance instance, bool enableValidationLayers)
    {
        _vk = vk;
        _surface = new VulkanSurface(window, vk, instance);
        _devices = new VulkanDevices(vk, instance, _surface, enableValidationLayers);
        _swapChain = new VulkanSwapChain(vk, instance, _surface, _devices, window.FramebufferSize);
        _pipeline = new VulkanPipeline(vk, _devices.LogicalDevice, _swapChain);
        _framebuffers = CreateFramebuffers(vk, _devices.LogicalDevice, _swapChain, _pipeline.RenderPass);

        // Synchronization.
        _commandPool = CreateCommandPool(vk, _devices.LogicalDevice, _devices.QueueFamilyIndices.GraphicsIndex);
        _commandBuffers = CreateCommandBuffers(vk, _commandPool, _devices.LogicalDevice, _swapChain.Extent, _pipeline, _framebuffers);
        _imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
        _imagesInFlight = new Fence[_swapChain.Images.Length];

        SetSyncObjects();
    }

    internal void DrawFrame(double delta)
    {
        _vk.WaitForFences(_devices.LogicalDevice, 1, in _inFlightFences![_currentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex = 0;
        _swapChain.KhrSwapChain.AcquireNextImage(_devices.LogicalDevice, _swapChain.SwapChain, ulong.MaxValue, _imageAvailableSemaphores[_currentFrame], default, ref imageIndex);

        if (_imagesInFlight![imageIndex].Handle != default)
        {
            _vk.WaitForFences(_devices.LogicalDevice, 1, in _imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);
        }
        _imagesInFlight[imageIndex] = _inFlightFences[_currentFrame];

        Semaphore* waitSemaphores = stackalloc[] { _imageAvailableSemaphores[_currentFrame] };
        PipelineStageFlags* waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        Semaphore* signalSemaphores = stackalloc[] { _renderFinishedSemaphores[_currentFrame] };
        CommandBuffer buffer = _commandBuffers[imageIndex];

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &buffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        _vk.ResetFences(_devices.LogicalDevice, 1, in _inFlightFences[_currentFrame]);

        if (_vk.QueueSubmit(_devices.GraphicsQueue, 1, in submitInfo, _inFlightFences[_currentFrame]) != Result.Success)
        {
            throw new Exception("Failed to submit draw command buffer.");
        }

        SwapchainKHR* swapChains = stackalloc[] { _swapChain.SwapChain };
        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapChains,
            PImageIndices = &imageIndex
        };

        _swapChain.KhrSwapChain.QueuePresent(_devices.PresentQueue, in presentInfo);

        _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
    }

    private void SetSyncObjects()
    {
        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
        {
            if (_vk.CreateSemaphore(_devices.LogicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                _vk.CreateSemaphore(_devices.LogicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                _vk.CreateFence(_devices.LogicalDevice, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new Exception("Failed to create synchronization objects for a frame.");
            }
        }
    }

    /// <summary>
    /// Creates a framebuffer (compatible with render pass) for all of the images in the swap chain, to 
    /// use the one that corresponds to the retrieved image at drawing time.
    /// </summary>
    private static Framebuffer[] CreateFramebuffers(Vk vk, Device logicalDevice, VulkanSwapChain swapChain, RenderPass renderPass)
    {
        Framebuffer[] framebuffers = new Framebuffer[swapChain.ImageViews.Length];

        for (int i = 0; i < swapChain.ImageViews.Length; i++)
        {
            ImageView attachment = swapChain.ImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = swapChain.Extent.Width,
                Height = swapChain.Extent.Height,
                Layers = 1
            };

            if (vk.CreateFramebuffer(logicalDevice, in framebufferInfo, null, out framebuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to create framebuffer.");
            }
        }

        return framebuffers;
    }

    private static CommandPool CreateCommandPool(Vk vk, Device logicalDevice, uint queueFamilyGraphicsIndex)
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyGraphicsIndex
        };

        if (vk.CreateCommandPool(logicalDevice, in poolInfo, null, out CommandPool commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool.");
        }

        return commandPool;
    }

    private static CommandBuffer[] CreateCommandBuffers(Vk vk, CommandPool commandPool, Device logicalDevice, Extent2D swapChainExtend, VulkanPipeline pipeline, Framebuffer[] framebuffers)
    {
        CommandBuffer[] commandBuffers = new CommandBuffer[framebuffers.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
        {
            if (vk.AllocateCommandBuffers(logicalDevice, in allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate command buffers.");
            }
        }

        for (int i = 0; i < commandBuffers.Length; i++)
        {
            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo
            };

            if (vk.BeginCommandBuffer(commandBuffers[i], in beginInfo) != Result.Success)
            {
                throw new Exception("Failed to begin recording command buffer.");
            }

            RenderPassBeginInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = pipeline.RenderPass,
                Framebuffer = framebuffers[i],
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = swapChainExtend
                }
            };

            ClearValue clearColor = new()
            {
                Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 }
            };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;

            vk.CmdBeginRenderPass(commandBuffers[i], &renderPassInfo, SubpassContents.Inline);
            vk.CmdBindPipeline(commandBuffers[i], PipelineBindPoint.Graphics, pipeline.GraphicsPipeline);
            vk.CmdDraw(commandBuffers[i], 3, 1, 0, 0);
            vk.CmdEndRenderPass(commandBuffers[i]);

            if (vk.EndCommandBuffer(commandBuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to record command buffer.");
            }
        }

        return commandBuffers;
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
                for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
                {
                    _vk.DestroySemaphore(_devices.LogicalDevice, _renderFinishedSemaphores![i], null);
                    _vk.DestroySemaphore(_devices.LogicalDevice, _imageAvailableSemaphores![i], null);
                    _vk.DestroyFence(_devices.LogicalDevice, _inFlightFences![i], null);
                }

                _vk.DestroyCommandPool(_devices.LogicalDevice, _commandPool, null);

                foreach (Framebuffer framebuffer in _framebuffers)
                {
                    _vk.DestroyFramebuffer(_devices.LogicalDevice, framebuffer, null);
                }

                _pipeline.Dispose();
                _swapChain.Dispose();
                _devices.Dispose();
                _surface.Dispose();
            }

            _disposed = true;
        }
    }
}
