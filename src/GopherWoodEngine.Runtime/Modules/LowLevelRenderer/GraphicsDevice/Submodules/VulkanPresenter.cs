using Silk.NET.Maths;
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
    private readonly IWindow _window;
    private readonly Vk _vk;
    private readonly Instance _instance;
    private VulkanSwapChain _swapChain;
    private VulkanPipeline _pipeline;
    private VulkanSynchronization _sync;
    private int _currentFrame = 0;
    private bool _frameBufferResized = false;
    private bool _disposed = false;

    public VulkanPresenter(IWindow window, Vk vk, Instance instance, bool enableValidationLayers)
    {
        _window = window;
        _vk = vk;
        _instance = instance;
        _surface = new VulkanSurface(window, vk, instance);
        _devices = new VulkanDevices(vk, instance, _surface, enableValidationLayers);
        _swapChain = new VulkanSwapChain(vk, instance, _surface, _devices, window.FramebufferSize);
        _pipeline = new VulkanPipeline(vk, _devices.LogicalDevice, _swapChain);
        _sync = new VulkanSynchronization(vk, _devices, _swapChain, _pipeline, _devices.QueueFamilyIndices.GraphicsIndex);

        _window.Resize += OnWindowResize;
    }

    internal void DrawFrame(double delta)
    {
        bool presentSuccessful = _sync.Present(_devices.GraphicsQueue, _devices.PresentQueue, _swapChain, _currentFrame);

        if (presentSuccessful || _frameBufferResized)
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

        _vk.DeviceWaitIdle(_devices.LogicalDevice);

        _pipeline.Dispose();
        _swapChain.Dispose();

        _swapChain = new VulkanSwapChain(_vk, _instance, _surface, _devices, _window.FramebufferSize);
        _pipeline = new VulkanPipeline(_vk, _devices.LogicalDevice, _swapChain);

        _sync.Reset(_swapChain, _pipeline);
    }

    private void OnWindowResize(Vector2D<int> obj)
    {
        _frameBufferResized = true;
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
                _window.Resize -= OnWindowResize;

                _sync.Dispose();
                _pipeline.Dispose();
                _swapChain.Dispose();
                _devices.Dispose();
                _surface.Dispose();
            }

            _disposed = true;
        }
    }
}
