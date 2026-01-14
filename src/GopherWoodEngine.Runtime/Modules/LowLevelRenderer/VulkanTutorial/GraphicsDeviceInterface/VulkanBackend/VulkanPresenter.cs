using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VulkanTutorial.GraphicsDeviceInterface.VulkanBackend;

internal unsafe sealed class VulkanPresenter : IDisposable
{
    private const int MAX_FRAMES_IN_FLIGHT = 2;

    internal VulkanDevices Devices { get; }

    private readonly IWindow _window;
    private readonly Vk _vk;
    private readonly VulkanSurface _surface;
    private readonly VulkanSwapChain _swapChain;
    private readonly VulkanPipeline _pipeline;
    private readonly VulkanFrameContext _frameContext;
    private int _currentFrame = 0;
    private bool _frameBufferResized = false;
    private bool _isDisposed = false;

    public VulkanPresenter(IWindow window, VulkanAPI vulkanAPI, VulkanSurface surface)
    {
        Devices = new VulkanDevices(vulkanAPI, surface);

        _window = window;
        _vk = vulkanAPI.Vk;
        _surface = surface;
        _swapChain = new VulkanSwapChain(_vk, vulkanAPI.Instance, _surface, Devices, _window.FramebufferSize);
        _pipeline = new VulkanPipeline(_vk, Devices.LogicalDevice, _swapChain);
        _frameContext = new VulkanFrameContext(_vk, Devices, _swapChain, _pipeline, Devices.QueueFamilyIndices.GraphicsIndex);

        _window.FramebufferResize += OnFramebufferResize;
    }

    internal void DrawFrame(double delta)
    {
        //Silk Window has timing information so we are skipping the time code.
        float time = (float)_window.Time;

        bool presentSuccessful = _frameContext.Present(time, Devices.GraphicsQueue, Devices.PresentQueue, _swapChain, _currentFrame);

        if (!presentSuccessful || _frameBufferResized)
        {
            _frameBufferResized = false;
            ResetSwapChain();
        }

        _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
    }

    private void ResetSwapChain()
    {
        Vector2D<int> framebufferSize = _window.FramebufferSize;

        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = _window.FramebufferSize;
            _window.DoEvents();
        }

        _vk.DeviceWaitIdle(Devices.LogicalDevice);

        _frameContext.CleanUpSwapChain();
        _pipeline.CleanUpSwapChain();
        _swapChain.CleanUpSwapChain();
        _frameContext.CleanUpBuffers();

        //TODO: Right now I destroy the SwapChain, above, and then below I create a new one.
        //      A later optimization is to pass the old swapchain handle into the create call
        //      (OldSwapchain property of SwapchainCreateInfoKHR) and destroy it after successful creation.

        _swapChain.ResetSwapChain(framebufferSize);
        _pipeline.ResetSwapChain(_swapChain);
        _frameContext.ResetBuffers();
    }

    private void OnFramebufferResize(Vector2D<int> obj)
    {
        _frameBufferResized = true;
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
                _window.FramebufferResize -= OnFramebufferResize;

                _frameContext.Dispose();
                _pipeline.Dispose();
                _swapChain.Dispose();
                Devices.Dispose();
            }

            _isDisposed = true;
        }
    }
}
