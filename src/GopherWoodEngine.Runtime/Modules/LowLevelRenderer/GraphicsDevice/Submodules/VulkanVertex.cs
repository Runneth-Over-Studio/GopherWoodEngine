using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanVertex : IDisposable
{
    public Buffer Buffer { get; }
    public Vertex[] Vertices { get; }

    private readonly DeviceMemory _vertexBufferMemory;
    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private bool _disposed = false;

    public VulkanVertex(Vk vk, VulkanDevices devices)
    {
        _vk = vk;
        _devices = devices;

        Vertices =
        [
            new Vertex { Position = new Vector2D<float>(0.0f,-0.5f), Color = new Vector3D<float>(1.0f, 0.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f,0.5f), Color = new Vector3D<float>(0.0f, 1.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(-0.5f,0.5f), Color = new Vector3D<float>(0.0f, 0.0f, 1.0f) }
        ];

        Buffer = CreateVertexBuffer();
        _vertexBufferMemory = CreateVertexBufferMemory();
    }

    private Buffer CreateVertexBuffer()
    {
        BufferCreateInfo bufferInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = GetVertexBufferSize(),
            Usage = BufferUsageFlags.VertexBufferBit,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk.CreateBuffer(_devices.LogicalDevice, in bufferInfo, null, out Buffer vertexBuffer) != Result.Success)
        {
            throw new Exception("Failed to create vertex buffer.");
        }

        return vertexBuffer;
    }

    private DeviceMemory CreateVertexBufferMemory()
    {
        _vk.GetBufferMemoryRequirements(_devices.LogicalDevice, Buffer, out MemoryRequirements memRequirements);

        MemoryAllocateInfo allocateInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit)
        };

        if (_vk.AllocateMemory(_devices.LogicalDevice, in allocateInfo, null, out DeviceMemory vertexBufferMemory) != Result.Success)
        {
            throw new Exception("Failed to allocate vertex buffer memory.");
        }

        _vk.BindBufferMemory(_devices.LogicalDevice, Buffer, vertexBufferMemory, 0);

        void* data;
        _vk.MapMemory(_devices.LogicalDevice, vertexBufferMemory, 0, GetVertexBufferSize(), 0, &data);
        Vertices.AsSpan().CopyTo(new Span<Vertex>(data, Vertices.Length));
        _vk.UnmapMemory(_devices.LogicalDevice, vertexBufferMemory);

        return vertexBufferMemory;
    }

    private ulong GetVertexBufferSize()
    {
        return (ulong)(sizeof(Vertex) * Vertices.Length);
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

    protected virtual void Dispose(bool disposing)
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
