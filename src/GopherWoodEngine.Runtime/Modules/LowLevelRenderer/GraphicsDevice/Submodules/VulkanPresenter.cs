using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanPresenter : IDisposable
{
    private const int MAX_FRAMES_IN_FLIGHT = 2;

    internal VulkanDevices Devices { get; }

    private readonly IWindow _window;
    private readonly Vk _vk;
    private readonly VulkanSurface _surface;
    private readonly VulkanSwapChain _swapChain;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private readonly VulkanPipeline _pipeline;
    private readonly VulkanSynchronization _sync;
    private int _currentFrame = 0;
    private bool _frameBufferResized = false;
    private bool _disposed = false;

    public VulkanPresenter(IWindow window, Vk vk, Instance instance, bool enableValidationLayers)
    {
        _window = window;
        _vk = vk;
        _surface = new VulkanSurface(window, vk, instance);
        Devices = new VulkanDevices(vk, instance, _surface, enableValidationLayers);
        _swapChain = new VulkanSwapChain(vk, instance, _surface, Devices, window.FramebufferSize);
        _descriptorSetLayout = CreateDescriptorSetLayout(_vk, Devices.LogicalDevice);
        _pipeline = new VulkanPipeline(vk, Devices.LogicalDevice, _swapChain, _descriptorSetLayout);
        _sync = new VulkanSynchronization(vk, Devices, _swapChain, _pipeline, Devices.QueueFamilyIndices.GraphicsIndex);

        _window.Resize += OnWindowResize;
    }

    internal void DrawFrame(double delta)
    {
        //Silk Window has timing information so we are skipping the time code.
        float time = (float)_window.Time;

        bool presentSuccessful = _sync.Present(time, Devices.GraphicsQueue, Devices.PresentQueue, _swapChain, _currentFrame);

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

        _sync.CleanUpSwapChain();
        _pipeline.CleanUpSwapChain();
        _swapChain.CleanUpSwapChain();
        _sync.CleanUpBuffers();

        _swapChain.ResetSwapChain();
        _pipeline.ResetSwapChain(_swapChain);
        _sync.ResetBuffers();
    }

    private void OnWindowResize(Vector2D<int> obj)
    {
        _frameBufferResized = true;
    }

    private static DescriptorSetLayout CreateDescriptorSetLayout(Vk vk, Device logicalDevice)
    {
        DescriptorSetLayoutBinding uboLayoutBinding = new()
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            PImmutableSamplers = null,
            StageFlags = ShaderStageFlags.VertexBit
        };

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &uboLayoutBinding
        };

        if (vk.CreateDescriptorSetLayout(logicalDevice, in layoutInfo, null, out DescriptorSetLayout descriptorSetLayout) != Result.Success)
        {
            throw new Exception("Failed to create descriptor set layout.");
        }

        return descriptorSetLayout;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    internal void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _window.Resize -= OnWindowResize;

                _sync.Dispose();
                _pipeline.Dispose();
                _vk.DestroyDescriptorSetLayout(Devices.LogicalDevice, _descriptorSetLayout, null);
                _swapChain.Dispose();
                Devices.Dispose();
                _surface.Dispose();
            }

            _disposed = true;
        }
    }
}
