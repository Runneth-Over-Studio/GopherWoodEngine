using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VulkanTutorial.GraphicsDeviceInterface;

/// <summary>
/// Represents a graphics device that provides windowing abstractions and functionality for interfacing with the GPU.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/> to ensure proper cleanup of resources.
/// </remarks>
public interface IGraphicsDeviceInterface : IDisposable
{
    /// <summary>
    /// Waits for the logical device to become idle, ensuring that all queued work on the device has finished before proceeding.
    /// </summary>
    /// <remarks>
    /// This method should be called before performing operations that require the GPU to be in an idle state,
    /// such as cleanup, resource destruction, or swapchain recreation.
    /// </remarks>
    void WaitIdle();
}
