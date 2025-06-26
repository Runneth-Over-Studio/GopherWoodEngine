using Silk.NET.Vulkan;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanSynchronization : IDisposable
{
    private const int MAX_FRAMES_IN_FLIGHT = 2;

    private readonly CommandPool _commandPool;
    private readonly Semaphore[] _imageAvailableSemaphores;
    private readonly Semaphore[] _renderFinishedSemaphores;
    private readonly Fence[] _inFlightFences;
    private readonly Vk _vk;
    private readonly Device _logicalDevice;
    private Framebuffer[] _framebuffers;
    private CommandBuffer[] _commandBuffers;
    private Fence[] _imagesInFlight;
    private bool _disposed = false;

    public VulkanSynchronization(Vk vk, Device logicalDevice, VulkanSwapChain swapChain, VulkanPipeline pipeline, uint queueFamilyGraphicsIndex)
    {
        _vk = vk;
        _logicalDevice = logicalDevice;
        _framebuffers = CreateFramebuffers(vk, logicalDevice, swapChain, pipeline.RenderPass);
        _commandPool = CreateCommandPool(vk, logicalDevice, queueFamilyGraphicsIndex);
        _commandBuffers = CreateCommandBuffers(vk, _commandPool, logicalDevice, swapChain.Extent, pipeline, _framebuffers);
        _imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
        _imagesInFlight = new Fence[swapChain.Images.Length];

        SetSyncObjects();
    }

    internal bool Present(Queue graphicsQueue, Queue presentQueue, VulkanSwapChain swapChain, int currentFrame)
    {
        _vk.WaitForFences(_logicalDevice, 1, in _inFlightFences![currentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex = 0;
        Result result = swapChain.KhrSwapChain.AcquireNextImage(_logicalDevice, swapChain.SwapChain, ulong.MaxValue, _imageAvailableSemaphores[currentFrame], default, ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            return false;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("Failed to acquire swap chain image.");
        }

        if (_imagesInFlight![imageIndex].Handle != default)
        {
            _vk.WaitForFences(_logicalDevice, 1, in _imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);
        }
        _imagesInFlight[imageIndex] = _inFlightFences[currentFrame];

        Semaphore* waitSemaphores = stackalloc[] { _imageAvailableSemaphores[currentFrame] };
        PipelineStageFlags* waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        Semaphore* signalSemaphores = stackalloc[] { _renderFinishedSemaphores[currentFrame] };
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

        _vk.ResetFences(_logicalDevice, 1, in _inFlightFences[currentFrame]);

        if (_vk.QueueSubmit(graphicsQueue, 1, in submitInfo, _inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("Failed to submit draw command buffer.");
        }

        SwapchainKHR* swapChains = stackalloc[] { swapChain.SwapChain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapChains,
            PImageIndices = &imageIndex
        };

        result = swapChain.KhrSwapChain.QueuePresent(presentQueue, in presentInfo);

        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            return false;
        }
        else if (result != Result.Success)
        {
            throw new Exception("Failed to present swap chain image.");
        }

        return true;
    }

    internal void Reset(VulkanSwapChain swapChain, VulkanPipeline pipeline)
    {
        fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            _vk.FreeCommandBuffers(_logicalDevice, _commandPool, (uint)_commandBuffers.Length, commandBuffersPtr);
        }

        foreach (Framebuffer framebuffer in _framebuffers)
        {
            _vk.DestroyFramebuffer(_logicalDevice, framebuffer, null);
        }

        _framebuffers = CreateFramebuffers(_vk, _logicalDevice, swapChain, pipeline.RenderPass);
        _commandBuffers = CreateCommandBuffers(_vk, _commandPool, _logicalDevice, swapChain.Extent, pipeline, _framebuffers);
        _imagesInFlight = new Fence[swapChain.Images.Length];
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
            if (_vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                _vk.CreateSemaphore(_logicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                _vk.CreateFence(_logicalDevice, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
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
                    _vk.DestroySemaphore(_logicalDevice, _renderFinishedSemaphores![i], null);
                    _vk.DestroySemaphore(_logicalDevice, _imageAvailableSemaphores![i], null);
                    _vk.DestroyFence(_logicalDevice, _inFlightFences![i], null);
                }

                fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
                {
                    _vk.FreeCommandBuffers(_logicalDevice, _commandPool, (uint)_commandBuffers.Length, commandBuffersPtr);
                }

                _vk.DestroyCommandPool(_logicalDevice, _commandPool, null);

                foreach (Framebuffer framebuffer in _framebuffers)
                {
                    _vk.DestroyFramebuffer(_logicalDevice, framebuffer, null);
                }
            }

            _disposed = true;
        }
    }
}
