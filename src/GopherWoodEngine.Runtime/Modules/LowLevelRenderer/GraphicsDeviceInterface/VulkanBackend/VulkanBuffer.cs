using Silk.NET.Vulkan;
using System;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;

/// <summary>
/// Provides a generic, type-safe wrapper for Vulkan buffer objects.
/// </summary>
/// <typeparam name="T">The unmanaged data type stored in the buffer.</typeparam>
/// <remarks>
/// Memory can be mapped persistently or temporarily depending on usage patterns. For uniform buffers,
/// persistent mapping is recommended for frequent updates.
/// </remarks>
internal unsafe sealed class VulkanBuffer<T> : IDisposable where T : unmanaged
{
    /// <summary>
    /// Gets the number of elements of type <typeparamref name="T"/> that the buffer can hold.
    /// </summary>
    internal uint Count { get; }

    /// <summary>
    /// Gets the underlying Vulkan buffer handle.
    /// </summary>
    internal Buffer Handle { get; }

    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly ulong _instanceSize;
    private readonly ulong _size;
    private readonly DeviceMemory _memory;
    private void* _pData = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBuffer{T}"/> class with the specified capacity.
    /// </summary>
    /// <param name="count">The number of elements the buffer should hold.</param>
    /// <param name="usage">The intended usage of the buffer.</param>
    /// <param name="properties">The memory properties for the buffer.</param>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <remarks>
    /// The buffer is created but not initialized with data. Use <see cref="Map"/> and <see cref="Store(ReadOnlySpan{T}, uint)"/>
    /// to populate the buffer with data.
    /// </remarks>
    public unsafe VulkanBuffer(uint count, BufferUsageFlags usage, MemoryPropertyFlags properties, Vk vk, VulkanDevices devices)
    {
        _vk = vk;
        _devices = devices;
        _instanceSize = (ulong)Marshal.SizeOf<T>();
        _size = count * _instanceSize;

        Count = count;
        Handle = CreateBuffer(usage, properties, out _memory);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="VulkanBuffer{T}"/> class and populates it with initial data.
    /// </summary>
    /// <param name="data">The initial data to store in the buffer.</param>
    /// <param name="usage">The intended usage of the buffer.</param>
    /// <param name="properties">The memory properties for the buffer.</param>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <remarks>
    /// The buffer is created with a capacity equal to the length of the provided data,
    /// and the data is immediately copied into the buffer. The buffer is unmapped after initialization.
    /// </remarks>
    public unsafe VulkanBuffer(ReadOnlySpan<T> data, BufferUsageFlags usage, MemoryPropertyFlags properties, Vk vk, VulkanDevices devices) : this((uint)data.Length, usage, properties, vk, devices)
    {
        Map();
        Store(data);
        Unmap();
    }

    /// <summary>
    /// Maps the buffer's memory to CPU-accessible address space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After mapping, the buffer's data can be accessed directly through methods like <see cref="Store(ReadOnlySpan{T}, uint)"/>,
    /// <see cref="GetAt"/>, and the indexer property.
    /// </para>
    /// <para>
    /// The buffer must be unmapped using <see cref="Unmap"/> when CPU access is no longer needed.
    /// Leaving buffers mapped can impact performance and may exhaust available mapped memory.
    /// </para>
    /// <para>
    /// For uniform buffers that are updated frequently, it's acceptable to keep them persistently mapped.
    /// </para>
    /// </remarks>
    internal unsafe void Map()
    {
        VulkanUtilities.AssertVk(_vk.MapMemory(_devices.LogicalDevice, _memory, 0, _size, 0, ref _pData));
    }

    /// <summary>
    /// Unmaps the buffer's memory from CPU-accessible address space.
    /// </summary>
    /// <remarks>
    /// After unmapping, CPU access methods will throw <see cref="InvalidOperationException"/>.
    /// The buffer must be remapped using <see cref="Map"/> to restore CPU access.
    /// </remarks>
    internal void Unmap()
    {
        _vk.UnmapMemory(_devices.LogicalDevice, _memory);
        _pData = null;
    }

    /// <summary>
    /// Stores data into the mapped buffer at the specified offset.
    /// </summary>
    /// <param name="data">The data to store in the buffer.</param>
    /// <param name="offset">The offset in elements from the beginning of the buffer. Default is 0.</param>
    /// <remarks>
    /// The buffer must be mapped before calling this method. If the data extends beyond the buffer's
    /// capacity, the behavior is undefined and may result in memory corruption.
    /// </remarks>
    internal void Store(ReadOnlySpan<T> data, uint offset = 0)
    {
        data.CopyTo(new Span<T>((T*)_pData + offset, (int)Count));
    }

    /// <summary>
    /// Stores data into the mapped buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in elements from the beginning of the buffer.</param>
    /// <param name="data">The data to store in the buffer.</param>
    /// <remarks>
    /// This is a convenience overload that accepts an array. The buffer must be mapped before calling this method.
    /// </remarks>
    internal void Store(uint offset, params T[] data) => Store((ReadOnlySpan<T>)data, offset);

    /// <summary>
    /// Stores data into the mapped buffer starting at offset 0.
    /// </summary>
    /// <param name="data">The data to store in the buffer.</param>
    /// <remarks>
    /// This is a convenience overload that accepts an array. The buffer must be mapped before calling this method.
    /// </remarks>
    internal void Store(params T[] data) => Store((ReadOnlySpan<T>)data);

    /// <summary>
    /// Retrieves an element from the mapped buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in elements from the beginning of the buffer.</param>
    /// <returns>The element at the specified offset.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the buffer is not currently mapped.
    /// </exception>
    /// <remarks>
    /// The buffer must be mapped before calling this method. For better performance when accessing
    /// multiple elements, consider using <see cref="PtrTo"/> to get a pointer and perform direct memory access.
    /// </remarks>
    internal T GetAt(uint offset)
    {
        if (_pData == null)
        {
            throw new InvalidOperationException("Tried to get from unmapped buffer.");
        }

        return *(((T*)_pData) + offset);
    }

    /// <summary>
    /// Gets a pointer to an element in the mapped buffer at the specified offset.
    /// </summary>
    /// <param name="offset">The offset in elements from the beginning of the buffer.</param>
    /// <returns>A pointer to the element at the specified offset.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the buffer is not currently mapped.
    /// </exception>
    /// <remarks>
    /// The buffer must be mapped before calling this method. The returned pointer is valid only
    /// while the buffer remains mapped. Exercise caution when using the pointer to avoid buffer overruns.
    /// </remarks>
    internal T* PtrTo(uint offset)
    {
        if (_pData == null)
        {
            throw new InvalidOperationException("Tried to get pointer to unmapped buffer.");
        }

        return ((T*)_pData) + offset;
    }

    /// <summary>
    /// Gets or sets an element at the specified index in the mapped buffer.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the buffer is not currently mapped.
    /// </exception>
    /// <remarks>
    /// The buffer must be mapped before using this indexer. This provides convenient array-like
    /// access to buffer elements.
    /// </remarks>
    internal T this[uint index]
    {
        get => GetAt(index);
        set => Store(index, value);
    }

    /// <summary>
    /// Flushes mapped memory ranges to make writes visible to the GPU.
    /// </summary>
    /// <param name="offset">The offset in elements from the beginning of the buffer. Default is 0.</param>
    /// <param name="count">The number of elements to flush. If 0, flushes the entire buffer. Default is 0.</param>
    /// <remarks>
    /// <para>
    /// This method is necessary when using non-coherent memory (memory without <see cref="MemoryPropertyFlags.HostCoherentBit"/>).
    /// For coherent memory, this call is not required but is harmless.
    /// </para>
    /// <para>
    /// Flushing ensures that CPU writes to mapped memory are visible to the GPU before the buffer
    /// is used in rendering or compute operations.
    /// </para>
    /// </remarks>
    internal void Flush(uint offset = 0, uint count = 0)
    {
        MappedMemoryRange flushRange = new()
        {
            Memory = _memory,
            Offset = offset * _instanceSize,
            Size = count == 0 ? Vk.WholeSize : _instanceSize * count
        };

        VulkanUtilities.AssertVk(_vk.FlushMappedMemoryRanges(_devices.LogicalDevice, 1, in flushRange));
    }

    /// <summary>
    /// Copies data from this buffer to another buffer using GPU transfer operations.
    /// </summary>
    /// <param name="other">The destination buffer.</param>
    /// <param name="srcPosition">The source offset in elements. Default is 0.</param>
    /// <param name="dstPosition">The destination offset in elements. Default is 0.</param>
    /// <param name="length">The number of elements to copy. If 0, copies the entire buffer. Default is 0.</param>
    /// <exception cref="OverflowException">
    /// Thrown when the destination buffer's capacity is smaller than this buffer's capacity.
    /// </exception>
    /// <remarks>
    /// <para>
    /// This is a synchronous operation that creates a temporary command buffer, submits the copy command,
    /// and waits for completion. For better performance in production code, consider batching multiple
    /// transfers or using asynchronous transfer queues.
    /// </para>
    /// <para>
    /// Both buffers must have been created with appropriate transfer flags (<see cref="BufferUsageFlags.TransferSrcBit"/>
    /// for the source and <see cref="BufferUsageFlags.TransferDstBit"/> for the destination).
    /// </para>
    /// </remarks>
    internal void CopyTo(VulkanBuffer<T> other, uint srcPosition = 0, uint dstPosition = 0, uint length = 0)
    {
        if (other.Count < Count)
        {
            throw new OverflowException("Buffer count not sufficient as copy destination.");
        }

        CommandBuffer commandBuffer = _devices.BeginSingleUseCommandBuffer(_devices.GraphicsCommandPool);

        BufferCopy copyRegion = new()
        {
            SrcOffset = srcPosition * _instanceSize,
            DstOffset = dstPosition * _instanceSize,
            Size = length == 0 ? _size : length * _instanceSize
        };

        _vk.CmdCopyBuffer(commandBuffer, Handle, other.Handle, 1, in copyRegion);

        _devices.EndSingleUseCommandBuffer(commandBuffer, _devices.GraphicsQueue, _devices.GraphicsCommandPool);
    }

    /// <summary>
    /// Creates a descriptor buffer info structure for use in descriptor sets.
    /// </summary>
    /// <param name="offset">The offset in elements from the beginning of the buffer. Default is 0.</param>
    /// <param name="range">The range in elements. If 0, uses the entire buffer. Default is 0.</param>
    /// <returns>A <see cref="DescriptorBufferInfo"/> structure describing this buffer.</returns>
    /// <remarks>
    /// This method is typically used when creating or updating descriptor sets that reference this buffer,
    /// such as for uniform buffers or storage buffers.
    /// </remarks>
    internal DescriptorBufferInfo DescriptorInfo(ulong offset = 0, ulong range = 0)
    {
        return new DescriptorBufferInfo()
        {
            Buffer = Handle,
            Offset = offset * _instanceSize,
            Range = range == 0 ? _size : range * _instanceSize
        };
    }

    /// <summary>
    /// Creates a vertex buffer optimized for GPU-local access and populates it with the provided data.
    /// </summary>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <param name="data">The vertex data to store in the buffer.</param>
    /// <returns>A new <see cref="VulkanBuffer{T}"/> configured as a vertex buffer.</returns>
    /// <remarks>
    /// <para>
    /// This method uses a staging buffer pattern for optimal performance:
    /// </para>
    /// <list type="number">
    /// <item><description>Creates a host-visible staging buffer and copies data to it</description></item>
    /// <item><description>Creates a device-local vertex buffer</description></item>
    /// <item><description>Transfers data from staging to vertex buffer via GPU copy</description></item>
    /// <item><description>Disposes the staging buffer</description></item>
    /// </list>
    /// <para>
    /// Device-local memory provides the best performance for data accessed frequently by the GPU.
    /// </para>
    /// </remarks>
    internal static VulkanBuffer<T> CreateVertexBuffer(Vk vk, VulkanDevices devices, params T[] data)
    {
        VulkanBuffer<T> stagingBuffer = new(
            data,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            vk,
            devices);

        VulkanBuffer<T> vertexBuffer = new(
            (uint)data.Length,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            vk,
            devices);

        stagingBuffer.CopyTo(vertexBuffer);
        stagingBuffer.Dispose();

        return vertexBuffer;
    }

    /// <summary>
    /// Creates an index buffer optimized for GPU-local access and populates it with the provided data.
    /// </summary>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <param name="data">The index data to store in the buffer.</param>
    /// <returns>A new <see cref="VulkanBuffer{T}"/> configured as an index buffer.</returns>
    /// <remarks>
    /// <para>
    /// This method uses a staging buffer pattern for optimal performance:
    /// </para>
    /// <list type="number">
    /// <item><description>Creates a host-visible staging buffer and copies data to it</description></item>
    /// <item><description>Creates a device-local index buffer</description></item>
    /// <item><description>Transfers data from staging to index buffer via GPU copy</description></item>
    /// <item><description>Disposes the staging buffer</description></item>
    /// </list>
    /// <para>
    /// Common index types include <see cref="ushort"/> (16-bit) and <see cref="uint"/> (32-bit).
    /// </para>
    /// </remarks>
    internal static VulkanBuffer<T> CreateIndexBuffer(Vk vk, VulkanDevices devices, params T[] data)
    {
        VulkanBuffer<T> stagingBuffer = new(
            data,
            BufferUsageFlags.TransferSrcBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            vk,
            devices);

        VulkanBuffer<T> indexBuffer = new(
            (uint)data.Length,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
            MemoryPropertyFlags.DeviceLocalBit,
            vk,
            devices);

        stagingBuffer.CopyTo(indexBuffer);
        stagingBuffer.Dispose();

        return indexBuffer;
    }

    /// <summary>
    /// Creates a uniform buffer in host-visible memory, pre-mapped for frequent CPU updates.
    /// </summary>
    /// <param name="vk">The Vulkan API instance.</param>
    /// <param name="devices">The Vulkan devices manager.</param>
    /// <returns>A new <see cref="VulkanBuffer{T}"/> configured as a persistently-mapped uniform buffer.</returns>
    /// <remarks>
    /// <para>
    /// The returned buffer is created with a capacity of 1 element and is already mapped,
    /// allowing immediate CPU writes without additional mapping calls.
    /// </para>
    /// <para>
    /// Uniform buffers are typically used for per-frame shader parameters such as transformation
    /// matrices, lighting data, and other frequently-updated constants. The persistent mapping
    /// pattern is ideal for data that changes every frame.
    /// </para>
    /// <para>
    /// The buffer uses host-coherent memory, so explicit flushing is not required after updates.
    /// </para>
    /// </remarks>
    internal static VulkanBuffer<T> CreateUniformBuffer(Vk vk, VulkanDevices devices)
    {
        VulkanBuffer<T> uniformBuffer = new(
            1,
            BufferUsageFlags.UniformBufferBit,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit,
            vk,
            devices);

        uniformBuffer.Map();

        return uniformBuffer;
    }

    private unsafe Buffer CreateBuffer(BufferUsageFlags usage, MemoryPropertyFlags properties, out DeviceMemory memory)
    {
        BufferCreateInfo bufferInfo = new()
        {
            Size = _size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        if (_vk.CreateBuffer(_devices.LogicalDevice, in bufferInfo, null, out Buffer buffer) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create vertex buffer.");
        }

        MemoryRequirements memRequirements = _vk.GetBufferMemoryRequirements(_devices.LogicalDevice, buffer);

        MemoryAllocateInfo allocateInfo = new()
        {
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = VulkanUtilities.FindMemoryType(memRequirements.MemoryTypeBits, properties, _devices.PhysicalDeviceSpecs.PhysicalDeviceMemoryProperties)
        };

        if (_vk.AllocateMemory(_devices.LogicalDevice, in allocateInfo, null, out memory) != Result.Success)
        {
            throw new InvalidOperationException("Failed to allocate vertex buffer memory.");
        }

        VulkanUtilities.AssertVk(_vk.BindBufferMemory(_devices.LogicalDevice, buffer, memory, 0));

        return buffer;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_pData != null)
        {
            Unmap();
        }

        _vk.FreeMemory(_devices.LogicalDevice, _memory, null);
        _vk.DestroyBuffer(_devices.LogicalDevice, Handle, null);
    }
}
