using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.VirtualScreen.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Vulkan;

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
    private readonly VulkanFrameContext _frameContext;
    private int _currentFrame = 0;
    private bool _frameBufferResized = false;
    private bool _disposed = false;

    public VulkanPresenter(VulkanVirtualScreen virtualScreen)
    {
        Devices = new VulkanDevices(virtualScreen);

        _window = virtualScreen.Window;
        _vk = virtualScreen.Vk;
        _surface = virtualScreen.Surface;
        _swapChain = new VulkanSwapChain(_vk, virtualScreen.Instance, _surface, Devices, _window.FramebufferSize);
        _descriptorSetLayout = CreateDescriptorSetLayout(_vk, Devices.LogicalDevice);
        _pipeline = new VulkanPipeline(_vk, Devices.LogicalDevice, _swapChain, _descriptorSetLayout);
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
                _window.FramebufferResize -= OnFramebufferResize;

                _frameContext.Dispose();
                _pipeline.Dispose();
                _vk.DestroyDescriptorSetLayout(Devices.LogicalDevice, _descriptorSetLayout, null);
                _swapChain.Dispose();
                Devices.Dispose();
            }

            _disposed = true;
        }
    }
}
