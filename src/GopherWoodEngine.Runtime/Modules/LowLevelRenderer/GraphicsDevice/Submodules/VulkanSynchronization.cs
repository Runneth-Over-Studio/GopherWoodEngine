using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanSynchronization : IDisposable
{
    private const int MAX_FRAMES_IN_FLIGHT = 2;

    private readonly Vk _vk;
    private readonly VulkanDevices _devices;
    private readonly VulkanSwapChain _swapChain;
    private readonly VulkanPipeline _pipeline;
    private readonly CommandPool _commandPool;
    private readonly DescriptorSetLayout _descriptorSetLayout;
    private readonly Vertex[] _vertices;
    private readonly ushort[] _indices;
    private readonly Buffer _vertexBuffer;
    private readonly DeviceMemory _vertexBufferMemory;
    private readonly Buffer _indexBuffer;
    private readonly DeviceMemory _indexBufferMemory;
    private readonly Semaphore[] _imageAvailableSemaphores;
    private readonly Semaphore[] _renderFinishedSemaphores;
    private readonly Fence[] _inFlightFences;
    private Framebuffer[] _framebuffers;
    private Buffer[] _uniformBuffers;
    private DeviceMemory[] _uniformBuffersMemory;
    private DescriptorPool _descriptorPool;
    private DescriptorSet[] _descriptorSets;
    private CommandBuffer[] _commandBuffers;
    private Fence[] _imagesInFlight;
    private bool _disposed = false;

    public VulkanSynchronization(Vk vk, VulkanDevices devices, VulkanSwapChain swapChain, VulkanPipeline pipeline, uint queueFamilyGraphicsIndex)
    {
        _vk = vk;
        _devices = devices;
        _swapChain = swapChain;
        _pipeline = pipeline;

        _framebuffers = CreateFramebuffers(vk, devices.LogicalDevice, swapChain, pipeline.RenderPass);
        _commandPool = CreateCommandPool(vk, devices.LogicalDevice, queueFamilyGraphicsIndex);
        _descriptorSetLayout = pipeline.DescriptorSetLayout;

        _vertices =
        [
            new Vertex { Position = new Vector2D<float>(-0.5f, -0.5f), Color = new Vector3D<float>(1.0f, 0.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f, -0.5f), Color = new Vector3D<float>(0.0f, 1.0f, 0.0f) },
            new Vertex { Position = new Vector2D<float>(0.5f, 0.5f), Color = new Vector3D<float>(0.0f, 0.0f, 1.0f) },
            new Vertex { Position = new Vector2D<float>(-0.5f, 0.5f), Color = new Vector3D<float>(1.0f, 1.0f, 1.0f) }
        ];

        _indices = [0, 1, 2, 2, 3, 0];

        (_vertexBuffer, _vertexBufferMemory) = CreateBufferWithMemory(_vertices, BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit);
        (_indexBuffer, _indexBufferMemory) = CreateBufferWithMemory(_indices, BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit);
        (_uniformBuffers, _uniformBuffersMemory) = CreateUniformBuffers();

        _descriptorPool = CreateDescriptorPool(_vk, _devices.LogicalDevice, swapChain.Images.Length);
        _descriptorSets = CreateDescriptorSets(_vk, _devices.LogicalDevice, swapChain.Images.Length, pipeline.DescriptorSetLayout, _uniformBuffers, _descriptorPool);

        _commandBuffers = CreateCommandBuffers();
        _imageAvailableSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _renderFinishedSemaphores = new Semaphore[MAX_FRAMES_IN_FLIGHT];
        _inFlightFences = new Fence[MAX_FRAMES_IN_FLIGHT];
        _imagesInFlight = new Fence[swapChain.Images.Length];

        SetSyncObjects();
    }

    internal bool Present(float windowTime, Queue graphicsQueue, Queue presentQueue, VulkanSwapChain swapChain, int currentFrame)
    {
        _vk.WaitForFences(_devices.LogicalDevice, 1, in _inFlightFences[currentFrame], Vk.True, ulong.MaxValue);

        uint imageIndex = 0;
        Result result = swapChain.KhrSwapChain.AcquireNextImage(_devices.LogicalDevice, swapChain.SwapChain, ulong.MaxValue, _imageAvailableSemaphores[currentFrame], default, ref imageIndex);

        if (result == Result.ErrorOutOfDateKhr)
        {
            return false;
        }
        else if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("Failed to acquire swap chain image.");
        }

        UpdateUniformBuffer(windowTime, swapChain.Extent, imageIndex);

        if (_imagesInFlight![imageIndex].Handle != default)
        {
            _vk.WaitForFences(_devices.LogicalDevice, 1, in _imagesInFlight[imageIndex], Vk.True, ulong.MaxValue);
        }
        _imagesInFlight[imageIndex] = _inFlightFences[currentFrame];

        Semaphore* waitSemaphores = stackalloc[] { _imageAvailableSemaphores[currentFrame] };
        PipelineStageFlags* waitStages = stackalloc[] { PipelineStageFlags.ColorAttachmentOutputBit };
        Semaphore* signalSemaphores = stackalloc[] { _renderFinishedSemaphores[currentFrame] };
        CommandBuffer buffer = _commandBuffers[imageIndex];

        SubmitInfo submitInfo = new()
        {
            SType = StructureType.SubmitInfo,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = waitSemaphores,
            PWaitDstStageMask = waitStages,
            CommandBufferCount = 1,
            PCommandBuffers = &buffer,
            SignalSemaphoreCount = 1,
            PSignalSemaphores = signalSemaphores
        };

        _vk.ResetFences(_devices.LogicalDevice, 1, in _inFlightFences[currentFrame]);

        if (_vk.QueueSubmit(graphicsQueue, 1, in submitInfo, _inFlightFences[currentFrame]) != Result.Success)
        {
            throw new Exception("Failed to submit draw command buffer.");
        }

        SwapchainKHR* swapChains = stackalloc[] { swapChain.SwapChain };

        PresentInfoKHR presentInfo = new()
        {
            SType = StructureType.PresentInfoKhr,
            WaitSemaphoreCount = 1,
            PWaitSemaphores = signalSemaphores,
            SwapchainCount = 1,
            PSwapchains = swapChains,
            PImageIndices = &imageIndex
        };

        result = swapChain.KhrSwapChain.QueuePresent(presentQueue, in presentInfo);

        if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
        {
            return false;
        }
        else if (result != Result.Success)
        {
            throw new Exception("Failed to present swap chain image.");
        }

        return true;
    }

    internal void CleanUpSwapChain()
    {
        foreach (Framebuffer framebuffer in _framebuffers)
        {
            _vk.DestroyFramebuffer(_devices.LogicalDevice, framebuffer, null);
        }

        fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
        {
            _vk.FreeCommandBuffers(_devices.LogicalDevice, _commandPool, (uint)_commandBuffers.Length, commandBuffersPtr);
        }
    }

    internal void CleanUpBuffers()
    {
        for (int i = 0; i < _swapChain.Images.Length; i++)
        {
            _vk.DestroyBuffer(_devices.LogicalDevice, _uniformBuffers[i], null);
            _vk.FreeMemory(_devices.LogicalDevice, _uniformBuffersMemory[i], null);
        }

        _vk.DestroyDescriptorPool(_devices.LogicalDevice, _descriptorPool, null);
    }

    internal void ResetBuffers()
    {
        _framebuffers = CreateFramebuffers(_vk, _devices.LogicalDevice, _swapChain, _pipeline.RenderPass);

        (_uniformBuffers, _uniformBuffersMemory) = CreateUniformBuffers();
        _descriptorPool = CreateDescriptorPool(_vk, _devices.LogicalDevice, _swapChain.Images.Length);
        _descriptorSets = CreateDescriptorSets(_vk, _devices.LogicalDevice, _swapChain.Images.Length, _descriptorSetLayout, _uniformBuffers, _descriptorPool);

        _commandBuffers = CreateCommandBuffers();

        _imagesInFlight = new Fence[_swapChain.Images.Length];
    }

    private void SetSyncObjects()
    {
        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit
        };

        for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
        {
            if (_vk.CreateSemaphore(_devices.LogicalDevice, in semaphoreInfo, null, out _imageAvailableSemaphores[i]) != Result.Success ||
                _vk.CreateSemaphore(_devices.LogicalDevice, in semaphoreInfo, null, out _renderFinishedSemaphores[i]) != Result.Success ||
                _vk.CreateFence(_devices.LogicalDevice, in fenceInfo, null, out _inFlightFences[i]) != Result.Success)
            {
                throw new Exception("Failed to create synchronization objects for a frame.");
            }
        }
    }

    private CommandBuffer[] CreateCommandBuffers()
    {
        CommandBuffer[] commandBuffers = new CommandBuffer[_framebuffers.Length];

        CommandBufferAllocateInfo allocInfo = new()
        {
            SType = StructureType.CommandBufferAllocateInfo,
            CommandPool = _commandPool,
            Level = CommandBufferLevel.Primary,
            CommandBufferCount = (uint)commandBuffers.Length
        };

        fixed (CommandBuffer* commandBuffersPtr = commandBuffers)
        {
            if (_vk.AllocateCommandBuffers(_devices.LogicalDevice, in allocInfo, commandBuffersPtr) != Result.Success)
            {
                throw new Exception("Failed to allocate command buffers.");
            }
        }

        for (int i = 0; i < commandBuffers.Length; i++)
        {
            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo
            };

            if (_vk.BeginCommandBuffer(commandBuffers[i], in beginInfo) != Result.Success)
            {
                throw new Exception("Failed to begin recording command buffer.");
            }

            RenderPassBeginInfo renderPassInfo = new()
            {
                SType = StructureType.RenderPassBeginInfo,
                RenderPass = _pipeline.RenderPass,
                Framebuffer = _framebuffers[i],
                RenderArea =
                {
                    Offset = { X = 0, Y = 0 },
                    Extent = _swapChain.Extent
                }
            };

            ClearValue clearColor = new()
            {
                Color = new() { Float32_0 = 0, Float32_1 = 0, Float32_2 = 0, Float32_3 = 1 }
            };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = &clearColor;

            _vk.CmdBeginRenderPass(commandBuffers[i], &renderPassInfo, SubpassContents.Inline);
            _vk.CmdBindPipeline(commandBuffers[i], PipelineBindPoint.Graphics, _pipeline.GraphicsPipeline);

            Buffer[] vertexBuffers = [_vertexBuffer];
            ulong[] offsets = [0];

            fixed (ulong* offsetsPtr = offsets)
            fixed (Buffer* vertexBuffersPtr = vertexBuffers)
            {
                _vk.CmdBindVertexBuffers(commandBuffers[i], 0, 1, vertexBuffersPtr, offsetsPtr);
            }

            _vk.CmdBindIndexBuffer(commandBuffers[i], _indexBuffer, 0, IndexType.Uint16);
            _vk.CmdBindDescriptorSets(commandBuffers[i], PipelineBindPoint.Graphics, _pipeline.PipelineLayout, 0, 1, in _descriptorSets[i], 0, null);
            _vk.CmdDrawIndexed(commandBuffers[i], (uint)_indices.Length, 1, 0, 0, 0);
            _vk.CmdEndRenderPass(commandBuffers[i]);

            if (_vk.EndCommandBuffer(commandBuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to record command buffer.");
            }
        }

        return commandBuffers;
    }

    private void UpdateUniformBuffer(float windowTime, Extent2D swapChainExtent, uint currentImage)
    {
        UniformBufferObject ubo = new()
        {
            Model = Matrix4X4<float>.Identity * Matrix4X4.CreateFromAxisAngle<float>(new Vector3D<float>(0, 0, 1), windowTime * Radians(90.0f)),
            View = Matrix4X4.CreateLookAt(new Vector3D<float>(2, 2, 2), new Vector3D<float>(0, 0, 0), new Vector3D<float>(0, 0, 1)),
            Projection = Matrix4X4.CreatePerspectiveFieldOfView(Radians(45.0f), (float)swapChainExtent.Width / swapChainExtent.Height, 0.1f, 10.0f),
        };
        ubo.Projection.M22 *= -1;


        void* data;
        _vk.MapMemory(_devices.LogicalDevice, _uniformBuffersMemory[currentImage], 0, (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        _vk.UnmapMemory(_devices.LogicalDevice, _uniformBuffersMemory[currentImage]);

        static float Radians(float angle) => angle * MathF.PI / 180f;
    }

    private (Buffer, DeviceMemory) CreateBufferWithMemory<T>(T[] dataSource, BufferUsageFlags usage)
    {
        ulong bufferSize = (ulong)(Unsafe.SizeOf<T>() * dataSource.Length);

        Buffer stagingBuffer = VulkanUtilities.CreateBuffer(_vk, _devices.LogicalDevice, bufferSize, BufferUsageFlags.TransferSrcBit);
        DeviceMemory stagingBufferMemory = VulkanUtilities.CreateMemory(_vk, _devices, stagingBuffer, MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data;
        _vk.MapMemory(_devices.LogicalDevice, stagingBufferMemory, 0, bufferSize, 0, &data);
        dataSource.AsSpan().CopyTo(new Span<T>(data, dataSource.Length));
        _vk.UnmapMemory(_devices.LogicalDevice, stagingBufferMemory);

        Buffer buffer = VulkanUtilities.CreateBuffer(_vk, _devices.LogicalDevice, bufferSize, usage);
        DeviceMemory bufferMemory = VulkanUtilities.CreateMemory(_vk, _devices, buffer, MemoryPropertyFlags.DeviceLocalBit);

        CopyBuffer(_vk, _devices, _commandPool, stagingBuffer, buffer, bufferSize);

        _vk.DestroyBuffer(_devices.LogicalDevice, stagingBuffer, null);
        _vk.FreeMemory(_devices.LogicalDevice, stagingBufferMemory, null);

        return (buffer, bufferMemory);
    }

    private (Buffer[], DeviceMemory[]) CreateUniformBuffers()
    {
        ulong bufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();

        Buffer[] uniformBuffers = new Buffer[_swapChain.Images.Length];
        DeviceMemory[] uniformBuffersMemory = new DeviceMemory[_swapChain.Images.Length];

        for (int i = 0; i < _swapChain.Images.Length; i++)
        {
            uniformBuffers[i] = VulkanUtilities.CreateBuffer(_vk, _devices.LogicalDevice, bufferSize, BufferUsageFlags.UniformBufferBit);
            uniformBuffersMemory[i] = VulkanUtilities.CreateMemory(_vk, _devices, uniformBuffers[i], MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);
        }

        return (uniformBuffers, uniformBuffersMemory);
    }

    private static void CopyBuffer(Vk vk, VulkanDevices devices, CommandPool commandPool, Buffer srcBuffer, Buffer dstBuffer, ulong size)
    {
        CommandBuffer commandBuffer = VulkanUtilities.BeginSingleTimeCommands(vk, devices, commandPool);

        BufferCopy copyRegion = new()
        {
            Size = size
        };

        vk.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, in copyRegion);

        VulkanUtilities.EndSingleTimeCommands(vk, devices, commandPool, commandBuffer);
    }

    private static DescriptorPool CreateDescriptorPool(Vk vk, Device logicalDevice, int swapChainImagesLength)
    {
        DescriptorPoolSize poolSize = new()
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = (uint)swapChainImagesLength
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 1,
            PPoolSizes = &poolSize,
            MaxSets = (uint)swapChainImagesLength
        };

        if (vk.CreateDescriptorPool(logicalDevice, in poolInfo, null, out DescriptorPool descriptorPool) != Result.Success)
        {
            throw new Exception("Failed to create descriptor pool.");
        }

        return descriptorPool;
    }

    private static DescriptorSet[] CreateDescriptorSets(Vk vk, Device logicalDevice, int swapChainImagesLength, DescriptorSetLayout descriptorSetLayout, Buffer[] uniformBuffers, DescriptorPool descriptorPool)
    {
        DescriptorSet[] descriptorSets = new DescriptorSet[swapChainImagesLength];
        DescriptorSetLayout[] layouts = new DescriptorSetLayout[swapChainImagesLength];
        Array.Fill(layouts, descriptorSetLayout);

        fixed (DescriptorSetLayout* layoutsPtr = layouts)
        {
            DescriptorSetAllocateInfo allocateInfo = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = descriptorPool,
                DescriptorSetCount = (uint)swapChainImagesLength,
                PSetLayouts = layoutsPtr
            };

            fixed (DescriptorSet* descriptorSetsPtr = descriptorSets)
            {
                if (vk!.AllocateDescriptorSets(logicalDevice, in allocateInfo, descriptorSetsPtr) != Result.Success)
                {
                    throw new Exception("Failed to allocate descriptor sets.");
                }
            }
        }

        for (int i = 0; i < swapChainImagesLength; i++)
        {
            DescriptorBufferInfo bufferInfo = new()
            {
                Buffer = uniformBuffers[i],
                Offset = 0,
                Range = (ulong)Unsafe.SizeOf<UniformBufferObject>()
            };

            WriteDescriptorSet descriptorWrite = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = descriptorSets[i],
                DstBinding = 0,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.UniformBuffer,
                DescriptorCount = 1,
                PBufferInfo = &bufferInfo
            };

            vk.UpdateDescriptorSets(logicalDevice, 1, in descriptorWrite, 0, null);
        }

        return descriptorSets;
    }

    // Creates a framebuffer (compatible with render pass) for all of the images in the swap chain, to 
    // use the one that corresponds to the retrieved image at drawing time.
    private static Framebuffer[] CreateFramebuffers(Vk vk, Device logicalDevice, VulkanSwapChain swapChain, RenderPass renderPass)
    {
        Framebuffer[] framebuffers = new Framebuffer[swapChain.ImageViews.Length];

        for (int i = 0; i < swapChain.ImageViews.Length; i++)
        {
            ImageView attachment = swapChain.ImageViews[i];

            FramebufferCreateInfo framebufferInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = renderPass,
                AttachmentCount = 1,
                PAttachments = &attachment,
                Width = swapChain.Extent.Width,
                Height = swapChain.Extent.Height,
                Layers = 1
            };

            if (vk.CreateFramebuffer(logicalDevice, in framebufferInfo, null, out framebuffers[i]) != Result.Success)
            {
                throw new Exception("Failed to create framebuffer.");
            }
        }

        return framebuffers;
    }

    private static CommandPool CreateCommandPool(Vk vk, Device logicalDevice, uint queueFamilyGraphicsIndex)
    {
        CommandPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.CommandPoolCreateInfo,
            QueueFamilyIndex = queueFamilyGraphicsIndex
        };

        if (vk.CreateCommandPool(logicalDevice, in poolInfo, null, out CommandPool commandPool) != Result.Success)
        {
            throw new Exception("Failed to create command pool.");
        }

        return commandPool;
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
                for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
                {
                    _vk.DestroySemaphore(_devices.LogicalDevice, _renderFinishedSemaphores![i], null);
                    _vk.DestroySemaphore(_devices.LogicalDevice, _imageAvailableSemaphores![i], null);
                    _vk.DestroyFence(_devices.LogicalDevice, _inFlightFences![i], null);
                }

                fixed (CommandBuffer* commandBuffersPtr = _commandBuffers)
                {
                    _vk.FreeCommandBuffers(_devices.LogicalDevice, _commandPool, (uint)_commandBuffers.Length, commandBuffersPtr);
                }

                _vk.DestroyDescriptorPool(_devices.LogicalDevice, _descriptorPool, null);

                for (int i = 0; i < _swapChain.Images.Length; i++)
                {
                    _vk.DestroyBuffer(_devices.LogicalDevice, _uniformBuffers[i], null);
                    _vk.FreeMemory(_devices.LogicalDevice, _uniformBuffersMemory[i], null);
                }

                _vk.DestroyBuffer(_devices.LogicalDevice, _indexBuffer, null);
                _vk.FreeMemory(_devices.LogicalDevice, _indexBufferMemory, null);

                _vk.DestroyBuffer(_devices.LogicalDevice, _vertexBuffer, null);
                _vk.FreeMemory(_devices.LogicalDevice, _vertexBufferMemory, null);

                _vk.DestroyCommandPool(_devices.LogicalDevice, _commandPool, null);

                foreach (Framebuffer framebuffer in _framebuffers)
                {
                    _vk.DestroyFramebuffer(_devices.LogicalDevice, framebuffer, null);
                }
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

internal struct UniformBufferObject
{
    public Matrix4X4<float> Model;
    public Matrix4X4<float> View;
    public Matrix4X4<float> Projection;
}
