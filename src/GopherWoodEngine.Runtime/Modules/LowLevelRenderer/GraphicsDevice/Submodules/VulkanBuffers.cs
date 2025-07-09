using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanBuffers : IDisposable
{
    public Buffer VertexBuffer { get; }
    public Buffer IndexBuffer { get; }
    public Vertex[] Vertices { get; }
    public ushort[] Indices { get; }

    private readonly DeviceMemory _vertexBufferMemory;
    private readonly DeviceMemory _indexBufferMemory;
    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly CommandPool _commandPool;
    private bool _disposed = false;

    public VulkanBuffers(Vk vk, VulkanDevices devices, CommandPool commandPool)
    {
        _vk = vk;
        _devices = devices;
        _commandPool = commandPool;

        Vertices =
        [
            new Vertex { Position = new Vector2D<float>(-0.5f,-0.5f), Color = new Vector3D<float>(1.0f, 0.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f,-0.5f), Color = new Vector3D<float>(0.0f, 1.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f,0.5f), Color = new Vector3D<float>(0.0f, 0.0f, 1.0f) },
            new Vertex { Position = new Vector2D<float>(-0.5f,0.5f), Color = new Vector3D<float>(1.0f, 1.0f, 1.0f) }
        ];

        Indices = [0, 1, 2, 2, 3, 0];

        (VertexBuffer, _vertexBufferMemory) = CreateBufferWithMemory(Vertices, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit);
        (IndexBuffer, _indexBufferMemory) = CreateBufferWithMemory(Indices, BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit);
    }

    private (Buffer, DeviceMemory) CreateBufferWithMemory<T>(T[] dataSource, BufferUsageFlags usage)
    {
        ulong bufferSize = (ulong)(Unsafe.SizeOf<T>() * dataSource.Length);

        Buffer stagingBuffer = CreateBuffer(_vk, _devices.LogicalDevice, bufferSize, BufferUsageFlags.TransferSrcBit);
        DeviceMemory stagingBufferMemory = CreateMemory(_vk, _devices, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref stagingBuffer);

        void* data;
        _vk.MapMemory(_devices.LogicalDevice, stagingBufferMemory, 0, bufferSize, 0, &data);
        dataSource.AsSpan().CopyTo(new Span<T>(data, dataSource.Length));
        _vk.UnmapMemory(_devices.LogicalDevice, stagingBufferMemory);

        Buffer buffer = CreateBuffer(_vk, _devices.LogicalDevice, bufferSize, usage);
        DeviceMemory bufferMemory = CreateMemory(_vk, _devices, MemoryPropertyFlags.DeviceLocalBit, ref buffer);

        CopyBuffer(_vk, _devices, _commandPool, stagingBuffer, buffer, bufferSize);

        _vk.DestroyBuffer(_devices.LogicalDevice, stagingBuffer, null);
        _vk.FreeMemory(_devices.LogicalDevice, stagingBufferMemory, null);

        return (buffer, bufferMemory);
    }

    private static Buffer CreateBuffer(Vk vk, Device logicalDevice, ulong bufferSize, BufferUsageFlags usage)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufferSize,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (vk.CreateBuffer(logicalDevice, in bufferInfo, null, out Buffer vertexBuffer) != Result.Success)
        {
            throw new Exception("Failed to create vertex buffer.");
        }

        return vertexBuffer;
    }

    private static DeviceMemory CreateMemory(Vk vk, VulkanDevices devices, MemoryPropertyFlags properties, ref Buffer buffer)
    {
        vk.GetBufferMemoryRequirements(devices.LogicalDevice, buffer, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(vk, devices.PhysicalDevice, memRequirements.MemoryTypeBits, properties)
        };

        if (vk.AllocateMemory(devices.LogicalDevice, in allocateInfo, null, out DeviceMemory bufferMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate vertex buffer memory.");
        }

        vk.BindBufferMemory(devices.LogicalDevice, buffer, bufferMemory, 0);

        return bufferMemory;
    }

    private static void CopyBuffer(Vk vk, VulkanDevices devices, CommandPool commandPool, Buffer srcBuffer, Buffer dstBuffer, ulong size)
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = commandPool,
            CommandBufferCount = 1
        };

        vk.AllocateCommandBuffers(devices.LogicalDevice, in allocateInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        BufferCopy copyRegion = new()
        {
            Size = size
        };

        vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, in copyRegion);

        vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        vk.QueueSubmit(devices.GraphicsQueue, 1, in submitInfo, default);
        vk.QueueWaitIdle(devices.GraphicsQueue);

        vk.FreeCommandBuffers(devices.LogicalDevice, commandPool, 1, in commandBuffer);
    }

    private static uint FindMemoryType(Vk vk, PhysicalDevice physicalDevice, uint typeFilter, MemoryPropertyFlags properties)
    {
        vk.GetPhysicalDeviceMemoryProperties(physicalDevice, out PhysicalDeviceMemoryProperties memProperties);

        for (int i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new Exception("Failed to find suitable memory type.");
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
                _vk.DestroyBuffer(_devices.LogicalDevice, IndexBuffer, null);
                _vk.FreeMemory(_devices.LogicalDevice, _indexBufferMemory, null);

                _vk.DestroyBuffer(_devices.LogicalDevice, VertexBuffer, null);
                _vk.FreeMemory(_devices.LogicalDevice, _vertexBufferMemory, null);
            }

            _disposed = true;
        }
    }
}

internal struct Vertex
{
    public Vector2D<float> Position;
    public Vector3D<float> Color;

    public static VertexInputBindingDescription GetBindingDescription()
    {
        return new VertexInputBindingDescription()
        {
            Binding = 0,
            Stride = (uint)Unsafe.SizeOf<Vertex>(),
            InputRate = VertexInputRate.Vertex
        };
    }

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        return
        [
            new VertexInputAttributeDescription()
            {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32Sfloat,
                Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Position))
            },
            new VertexInputAttributeDescription()
            {
                Binding = 0,
                Location = 1,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Color))
            }
        ];
    }
}
