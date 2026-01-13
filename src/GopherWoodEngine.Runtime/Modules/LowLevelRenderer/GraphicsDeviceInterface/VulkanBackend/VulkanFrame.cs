using Silk.NET.Vulkan;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

/// <summary>
/// Represents a single frame in flight for Vulkan rendering operations.
/// </summary>
/// <remarks>
/// <para>
/// This class manages the synchronization primitives (semaphores and fences) and command buffer
/// for a single frame in a multi-buffered rendering setup. It coordinates the acquisition of
/// swapchain images, command buffer recording, and presentation.
/// </para>
/// <para>
/// Each frame operates independently with its own synchronization objects, allowing multiple
/// frames to be processed simultaneously (frames in flight) for better GPU utilization.
/// </para>
/// </remarks>
internal sealed class VulkanFrame : IDisposable
{
    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly VulkanSwapChainNew _swapChain;
    private readonly CommandBuffer _commandBuffer;
    private readonly Semaphore _imageAvailableSemaphore;
    private readonly Semaphore _renderFinishedSemaphore;
    private readonly Fence _inFlightFence;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanFrame"/> class.
    /// </summary>
    public VulkanFrame(Vk vk, VulkanDevices devices, VulkanSwapChainNew swapChain, CommandBuffer commandBuffer)
    {
        _vk = vk;
        _devices = devices;
        _swapChain = swapChain;
        _commandBuffer = commandBuffer;

        (Semaphore imageAvailableSemaphore, Semaphore renderFinishedSemaphore, Fence inFlightFence) = CreateSyncObjects(_vk, _devices.LogicalDevice);
        _imageAvailableSemaphore = imageAvailableSemaphore;
        _renderFinishedSemaphore = renderFinishedSemaphore;
        _inFlightFence = inFlightFence;
    }

    /// <summary>
    /// Begins a new frame by acquiring the next swapchain image and preparing the command buffer for recording.
    /// </summary>
    /// <returns>
    /// A <see cref="CommandBuffer"/> ready for recording rendering commands, or <c>null</c> if the
    /// swapchain needs to be recreated (e.g., due to window resize).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when fence waiting, fence resetting, command buffer operations fail.
    /// </exception>
    /// <remarks>
    /// If swapchain acquisition fails (returns <c>null</c>), the caller should handle swapchain recreation
    /// before attempting to render the next frame.
    /// </remarks>
    internal unsafe CommandBuffer? Begin()
    {
        if (_vk.WaitForFences(_devices.LogicalDevice, 1, in _inFlightFence, true, ulong.MaxValue) != Result.Success)
        {
            throw new InvalidOperationException("Failed to wait for fences.");
        }

        if (!_swapChain.AcquireNextImage(_imageAvailableSemaphore))
        {
            return null;
        }

        // Register that this frame's fence is now using the acquired swapchain image
        // This prevents semaphore reuse errors by tracking which frame is using which image
        _swapChain.RegisterImageUsage(_inFlightFence);

        if (_vk.ResetFences(_devices.LogicalDevice, 1, in _inFlightFence) != Result.Success)
        {
            throw new InvalidOperationException("Failed to reset fences.");
        }

        if (_vk.ResetCommandBuffer(_commandBuffer, 0) != Result.Success)
        {
            throw new InvalidOperationException("Failed to reset command buffer.");
        }

        CommandBufferBeginInfo beginInfo = new(flags: 0, pInheritanceInfo: null);
        if (_vk.BeginCommandBuffer(_commandBuffer, in beginInfo) != Result.Success)
        {
            throw new InvalidOperationException("Failed to begin command buffer.");
        }

        return _commandBuffer;
    }

    /// <summary>
    /// Ends the current frame by finishing command buffer recording, submitting it to the GPU,
    /// and presenting the rendered image to the swapchain.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when ending the command buffer or submitting to the graphics queue fails.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The synchronization ensures that:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Rendering doesn't begin until the swapchain image is available</description></item>
    /// <item><description>Presentation doesn't occur until rendering is complete</description></item>
    /// <item><description>The next frame doesn't reuse this command buffer until GPU work finishes</description></item>
    /// </list>
    /// </remarks>
    internal unsafe void End()
    {
        if (_vk.EndCommandBuffer(_commandBuffer) != Result.Success)
        {
            throw new InvalidOperationException("Failed to end recording command buffer.");
        }

        Semaphore* waitSemaphores = stackalloc Semaphore[1] { _imageAvailableSemaphore };
        Semaphore* signalSemaphores = stackalloc Semaphore[1] { _renderFinishedSemaphore };
        PipelineStageFlags* waitStages = stackalloc PipelineStageFlags[1] { PipelineStageFlags.ColorAttachmentOutputBit };

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        fixed (CommandBuffer* ptr = &_commandBuffer)
        {
            submitInfo.PCommandBuffers = ptr;
        }

        Result r;
        if ((r = _vk.QueueSubmit(_devices.GraphicsQueue, 1, in submitInfo, _inFlightFence)) != Result.Success)
        {
            throw new InvalidOperationException($"Failed to submit draw command buffer. Result: {r}");
        }

        _swapChain.PresentImage(_renderFinishedSemaphore);
    }

    private static unsafe (Semaphore imageAvailableSemaphore, Semaphore renderFinishedSemaphore, Fence inFlightFence) CreateSyncObjects(Vk vk, Device logicalDevice)
    {
        var semaphoreCreateInfo = new SemaphoreCreateInfo(sType: StructureType.SemaphoreCreateInfo);
        var fenceInfo = new FenceCreateInfo(flags: FenceCreateFlags.SignaledBit);

        if ((vk.CreateSemaphore(logicalDevice, in semaphoreCreateInfo, null, out Semaphore availableSemaphore) != Result.Success) ||
            (vk.CreateSemaphore(logicalDevice, in semaphoreCreateInfo, null, out Semaphore finishedSemaphore) != Result.Success) ||
            (vk.CreateFence(logicalDevice, in fenceInfo, null, out Fence flightFence) != Result.Success))
        {
            throw new InvalidOperationException("Failed to create synchronisation objects.");
        }

        return (availableSemaphore, finishedSemaphore, flightFence);
    }

    /// <inheritdoc/>
    public unsafe void Dispose()
    {
        _vk.DestroySemaphore(_devices.LogicalDevice, _imageAvailableSemaphore, null);
        _vk.DestroySemaphore(_devices.LogicalDevice, _renderFinishedSemaphore, null);
        _vk.DestroyFence(_devices.LogicalDevice, _inFlightFence, null);
    }
}