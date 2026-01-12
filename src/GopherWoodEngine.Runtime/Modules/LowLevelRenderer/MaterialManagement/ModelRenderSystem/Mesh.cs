using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using Silk.NET.Vulkan;
using System;

namespace GopherWoodEngine.Runtime.Modules;

internal class Mesh : IDisposable
{
    public VulkanBuffer<MeshVertex> VertexBuffer { get; }

    public VulkanBuffer<ushort> IndexBuffer { get; }

    private readonly VulkanGraphicsDeviceInterface _vkInterface;

    public Mesh(uint vertexCount, uint indexCount)
    {
        _vkInterface = Ioc.Default.GetRequiredService<VulkanGraphicsDeviceInterface>();

        VertexBuffer = new VulkanBuffer<MeshVertex>(
            vertexCount,
            BufferUsageFlags.VertexBufferBit | BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            _vkInterface.VulkanAPI.Vk,
            _vkInterface.Devices);

        IndexBuffer = new VulkanBuffer<ushort>(
            indexCount,
            BufferUsageFlags.IndexBufferBit | BufferUsageFlags.TransferDstBit,
            MemoryPropertyFlags.DeviceLocalBit,
            _vkInterface.VulkanAPI.Vk,
            _vkInterface.Devices);
    }

    public Mesh(MeshVertex[] vertices, ushort[] indices) : this((uint)vertices.Length, (uint)indices.Length)
    {
        _vkInterface = Ioc.Default.GetRequiredService<VulkanGraphicsDeviceInterface>();

        LoadVertices(vertices);
        LoadIndices(indices);
    }

    public void LoadVertices(params MeshVertex[] vertices)
    {
        if (vertices.Length > VertexBuffer.Count)
        {
            throw new ArgumentException("Too many vertices specified!");
        }

        VulkanBuffer<MeshVertex> stagingBuffer = StagingBuffer<MeshVertex>((uint)vertices.Length);

        stagingBuffer.Map();
        stagingBuffer.Store(vertices);
        stagingBuffer.CopyTo(VertexBuffer);
        stagingBuffer.Dispose();
    }

    public void LoadIndices(params ushort[] indices)
    {
        if (indices.Length > IndexBuffer.Count)
        {
            throw new ArgumentException("Too many indices specified.");
        }

        VulkanBuffer<ushort> stagingBuffer = StagingBuffer<ushort>((uint)indices.Length);

        stagingBuffer.Map();
        stagingBuffer.Store(indices);
        stagingBuffer.CopyTo(IndexBuffer);
        stagingBuffer.Dispose();
    }

    private VulkanBuffer<T> StagingBuffer<T>(uint count) where T : unmanaged
    {
        return new VulkanBuffer<T>(
            count,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            _vkInterface.VulkanAPI.Vk,
            _vkInterface.Devices);
    }

    public void Dispose()
    {
        VertexBuffer.Dispose();
        IndexBuffer.Dispose();
    }
}
