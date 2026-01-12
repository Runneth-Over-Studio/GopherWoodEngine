using Silk.NET.Vulkan;
using System;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

internal class VulkanUniformValue<T> : IDisposable where T : unmanaged
{
    private readonly VulkanSwapChainNew _swapChain;
    private readonly VulkanBuffer<T> _buffer;
    private readonly uint _memoryIndex;

    private readonly bool _dispose;

    public VulkanUniformValue(Vk vk, VulkanDevices devices, VulkanSwapChainNew swapChain)
    {
        _swapChain = swapChain;
        _memoryIndex = 0;
        _buffer = new VulkanBuffer<T>(
            VulkanSwapChainNew.MAX_FRAMES_IN_FLIGHT,
            BufferUsageFlags.UniformBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            vk,
            devices);
        _buffer.Map();
        _dispose = true;
    }

    public VulkanUniformValue(VulkanBuffer<T> buffer, uint memoryIndex, VulkanSwapChainNew swapChain)
    {
        _swapChain = swapChain;
        _memoryIndex = memoryIndex;
        _buffer = buffer;
        _dispose = false;
    }

    public void Set(T value)
    {
        _buffer.Store(_memoryIndex * VulkanSwapChainNew.MAX_FRAMES_IN_FLIGHT + (uint)_swapChain.CurrentFrameIndex, value);
    }

    public DescriptorBufferInfo BufferInfo()
    {
        return new DescriptorBufferInfo()
        {
            Buffer = _buffer.Handle,
            Offset = (ulong)(Marshal.SizeOf<T>() * (_memoryIndex * VulkanSwapChainNew.MAX_FRAMES_IN_FLIGHT + _swapChain.CurrentFrameIndex)),
            Range = (ulong)Marshal.SizeOf<T>()
        };
    }

    public void Dispose()
    {
        if (_dispose)
        {
            _buffer.Dispose();
        }
    }
}
