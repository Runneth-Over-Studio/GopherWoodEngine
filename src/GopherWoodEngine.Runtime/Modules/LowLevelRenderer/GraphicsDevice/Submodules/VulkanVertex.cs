using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanVertex : IDisposable
{
    public Buffer Buffer { get; }
    public Vertex[] Vertices { get; }

    private readonly DeviceMemory _vertexBufferMemory;
    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly CommandPool _commandPool;
    private bool _disposed = false;

    public VulkanVertex(Vk vk, VulkanDevices devices, CommandPool commandPool)
    {
        _vk = vk;
        _devices = devices;
        _commandPool = commandPool;

        Vertices =
        [
            new Vertex { Position = new Vector2D<float>(0.0f,-0.5f), Color = new Vector3D<float>(1.0f, 0.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f,0.5f), Color = new Vector3D<float>(0.0f, 1.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(-0.5f,0.5f), Color = new Vector3D<float>(0.0f, 0.0f, 1.0f) }
        ];

        (Buffer, _vertexBufferMemory) = CreateVertexBuffer();
    }

    private (Buffer, DeviceMemory) CreateVertexBuffer()
    {
        ulong bufferSize = (ulong)(sizeof(Vertex) * Vertices.Length);

        Buffer stagingBuffer = CreateBuffer(bufferSize, BufferUsageFlags.TransferSrcBit);
        DeviceMemory stagingBufferMemory = CreateMemory(MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit, ref stagingBuffer);

        void* data;
        _vk.MapMemory(_devices.LogicalDevice, stagingBufferMemory, 0, bufferSize, 0, &data);
        Vertices.AsSpan().CopyTo(new Span<Vertex>(data, Vertices.Length));
        _vk.UnmapMemory(_devices.LogicalDevice, stagingBufferMemory);

        Buffer vertexBuffer = CreateBuffer(bufferSize, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit);
        DeviceMemory vertexBufferMemory = CreateMemory(MemoryPropertyFlags.DeviceLocalBit, ref vertexBuffer);

        CopyBuffer(stagingBuffer, vertexBuffer, bufferSize);

        _vk.DestroyBuffer(_devices.LogicalDevice, stagingBuffer, null);
        _vk.FreeMemory(_devices.LogicalDevice, stagingBufferMemory, null);

        return (vertexBuffer, vertexBufferMemory);
    }

    private Buffer CreateBuffer(ulong bufferSize, BufferUsageFlags usage)
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = bufferSize,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk.CreateBuffer(_devices.LogicalDevice, in bufferInfo, null, out Buffer vertexBuffer) != Result.Success)
        {
            throw new Exception("Failed to create vertex buffer.");
        }

        return vertexBuffer;
    }

    private DeviceMemory CreateMemory(MemoryPropertyFlags properties, ref Buffer buffer)
    {
        _vk.GetBufferMemoryRequirements(_devices.LogicalDevice, buffer, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };

        if (_vk.AllocateMemory(_devices.LogicalDevice, in allocateInfo, null, out DeviceMemory bufferMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate vertex buffer memory.");
        }

        _vk.BindBufferMemory(_devices.LogicalDevice, buffer, bufferMemory, 0);

        return bufferMemory;
    }

    private void CopyBuffer(Buffer srcBuffer, Buffer dstBuffer, ulong size)
    {
        CommandBufferAllocateInfo allocateInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            Level = CommandBufferLevel.Primary,
            CommandPool = _commandPool,
            CommandBufferCount = 1
        };

        _vk.AllocateCommandBuffers(_devices.LogicalDevice, in allocateInfo, out CommandBuffer commandBuffer);

        CommandBufferBeginInfo beginInfo = new()
        {
            SType = StructureType.CommandBufferBeginInfo,
            Flags = CommandBufferUsageFlags.OneTimeSubmitBit
        };

        _vk.BeginCommandBuffer(commandBuffer, in beginInfo);

        BufferCopy copyRegion = new()
        {
            Size = size
        };

        _vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, in copyRegion);

        _vk.EndCommandBuffer(commandBuffer);

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            CommandBufferCount = 1,
            PCommandBuffers = &commandBuffer
        };

        _vk.QueueSubmit(_devices.GraphicsQueue, 1, in submitInfo, default);
        _vk.QueueWaitIdle(_devices.GraphicsQueue);

        _vk.FreeCommandBuffers(_devices.LogicalDevice, _commandPool, 1, in commandBuffer);
    }

    private uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        _vk.GetPhysicalDeviceMemoryProperties(_devices.PhysicalDevice, out PhysicalDeviceMemoryProperties memProperties);

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
                _vk.DestroyBuffer(_devices.LogicalDevice, Buffer, null);
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
