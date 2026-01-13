using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using GopherWoodEngine.Runtime.Modules.Rendering;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Vulkan-specific renderer for mesh renderables.
/// </summary>
internal sealed unsafe class VulkanMeshSubRenderer : ISubRenderer, IDisposable
{
    private readonly Vk _vk;
    private readonly VulkanGraphicsDeviceInterface _vkInterface;
    private readonly Dictionary<Guid, MeshGpuResources> _meshResources = new();
    private readonly Dictionary<string, ShaderPipeline> _pipelines = new();
    private readonly Dictionary<string, TextureResources> _textures = new();
    private DescriptorPool _descriptorPool;

    private const int MAX_DESCRIPTOR_SETS = 1000;

    private bool _isDisposed = false;

    public VulkanMeshSubRenderer(VulkanGraphicsDeviceInterface vkInterface)
    {
        _vkInterface = vkInterface;
        _vk = vkInterface.VulkanAPI.Vk;

        // Create descriptor pool
        _descriptorPool = CreateDescriptorPool();
    }

    public void Render(ICamera camera, CommandBuffer commandBuffer, IRenderable renderable)
    {
        if (renderable is not Rendering.Mesh mesh || !mesh.IsVisible)
        {
            return;
        }

        // 1. Ensure GPU resources exist for this mesh
        if (!_meshResources.TryGetValue(mesh.Id, out MeshGpuResources? gpuResources))
        {
            gpuResources = CreateMeshGpuResources(mesh);
            _meshResources[mesh.Id] = gpuResources;
        }

        // 2. Get or create pipeline for this shader
        string shaderName = mesh.Material?.Shader?.Name ?? "default";
        if (!_pipelines.TryGetValue(shaderName, out ShaderPipeline? pipeline))
        {
            pipeline = CreatePipeline(mesh.Material?.Shader, mesh.Material);
            _pipelines[shaderName] = pipeline;
        }

        // 2.5. Allocate descriptor set if needed (after pipeline exists)
        int frameIndex = _vkInterface.SwapChain.CurrentFrameIndex;
        if (gpuResources.DescriptorSets[frameIndex].Handle == 0)
        {
            gpuResources.DescriptorSets[frameIndex] = AllocateDescriptorSet(pipeline.DescriptorSetLayout);
        }

        // 3. Get or load texture if material has one
        TextureResources? texture = null;
        if (mesh.Material?.AlbedoTexture != null)
        {
            string texturePath = mesh.Material.AlbedoTexture.FilePath;
            if (!_textures.TryGetValue(texturePath, out texture))
            {
                texture = LoadTexture(texturePath);
                _textures[texturePath] = texture;
            }
        }

        // 4. Bind pipeline
        _vk.CmdBindPipeline(commandBuffer, PipelineBindPoint.Graphics, pipeline.Pipeline);

        // 5. Update and bind uniforms (view, projection, model matrices)
        UpdateUniformBuffer(camera, mesh, gpuResources);
        BindDescriptorSet(commandBuffer, pipeline, gpuResources, texture, frameIndex);

        // 6. Bind vertex and index buffers
        Buffer vertexBuffer = gpuResources.VertexBuffer;
        ulong offset = 0;
        _vk.CmdBindVertexBuffers(commandBuffer, 0, 1, &vertexBuffer, &offset);
        _vk.CmdBindIndexBuffer(commandBuffer, gpuResources.IndexBuffer, 0, IndexType.Uint32);

        // 7. Draw
        _vk.CmdDrawIndexed(commandBuffer, (uint)mesh.Indices.Length, 1, 0, 0, 0);
    }

    private MeshGpuResources CreateMeshGpuResources(Rendering.Mesh mesh)
    {
        // Create vertex buffer
        ulong vertexBufferSize = (ulong)(Unsafe.SizeOf<VulkanVertex>() * mesh.Vertices.Length);
        Buffer vertexBuffer = VulkanUtilities.CreateBuffer(_vk, _vkInterface.Devices.LogicalDevice, vertexBufferSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit);
        DeviceMemory vertexMemory = VulkanUtilities.CreateMemory(_vk, _vkInterface.Devices, vertexBuffer,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        // Upload vertex data
        VulkanVertex[] vulkanVertices = new VulkanVertex[mesh.Vertices.Length];
        for (int i = 0; i < mesh.Vertices.Length; i++)
        {
            vulkanVertices[i] = new VulkanVertex
            {
                Position = mesh.Vertices[i].Position,
                Normal = mesh.Vertices[i].Normal,
                Color = mesh.Vertices[i].Color,
                TexCoord = mesh.Vertices[i].TexCoord
            };
        }

        void* data;
        _vk.MapMemory(_vkInterface.Devices.LogicalDevice, vertexMemory, 0, vertexBufferSize, 0, &data);
        vulkanVertices.AsSpan().CopyTo(new Span<VulkanVertex>(data, vulkanVertices.Length));
        _vk.UnmapMemory(_vkInterface.Devices.LogicalDevice, vertexMemory);

        // Create index buffer
        ulong indexBufferSize = (ulong)(sizeof(uint) * mesh.Indices.Length);
        Buffer indexBuffer = VulkanUtilities.CreateBuffer(_vk, _vkInterface.Devices.LogicalDevice, indexBufferSize,
            BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit);
        DeviceMemory indexMemory = VulkanUtilities.CreateMemory(_vk, _vkInterface.Devices, indexBuffer,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        // Upload index data
        _vk.MapMemory(_vkInterface.Devices.LogicalDevice, indexMemory, 0, indexBufferSize, 0, &data);
        mesh.Indices.AsSpan().CopyTo(new Span<uint>(data, mesh.Indices.Length));
        _vk.UnmapMemory(_vkInterface.Devices.LogicalDevice, indexMemory);

        // Create uniform buffer
        ulong uniformBufferSize = (ulong)Unsafe.SizeOf<UniformBufferObject>();
        Buffer uniformBuffer = VulkanUtilities.CreateBuffer(_vk, _vkInterface.Devices.LogicalDevice, uniformBufferSize,
            BufferUsageFlags.UniformBufferBit);
        DeviceMemory uniformMemory = VulkanUtilities.CreateMemory(_vk, _vkInterface.Devices, uniformBuffer,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        return new MeshGpuResources
        {
            VertexBuffer = vertexBuffer,
            VertexMemory = vertexMemory,
            IndexBuffer = indexBuffer,
            IndexMemory = indexMemory,
            UniformBuffer = uniformBuffer,
            UniformMemory = uniformMemory,
            DescriptorSets = new DescriptorSet[VulkanSwapChainNew.MAX_FRAMES_IN_FLIGHT]
        };
    }

    private TextureResources LoadTexture(string imagePath)
    {
        // Load image from file
        using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(imagePath);
        ulong imageSize = (ulong)(img.Width * img.Height * img.PixelType.BitsPerPixel / 8);

        // Create staging buffer
        Buffer stagingBuffer = VulkanUtilities.CreateBuffer(_vk, _vkInterface.Devices.LogicalDevice, imageSize, BufferUsageFlags.TransferSrcBit);
        DeviceMemory stagingMemory = VulkanUtilities.CreateMemory(_vk, _vkInterface.Devices, stagingBuffer,
            MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        // Upload image data to staging buffer
        void* data;
        _vk.MapMemory(_vkInterface.Devices.LogicalDevice, stagingMemory, 0, imageSize, 0, &data);
        img.CopyPixelDataTo(new Span<byte>(data, (int)imageSize));
        _vk.UnmapMemory(_vkInterface.Devices.LogicalDevice, stagingMemory);

        // Create image
        ImageCreateInfo imageInfo = new()
        {
            SType = StructureType.ImageCreateInfo,
            ImageType = ImageType.Type2D,
            Format = Format.R8G8B8A8Srgb,
            Extent = new Extent3D((uint)img.Width, (uint)img.Height, 1),
            MipLevels = 1,
            ArrayLayers = 1,
            Samples = SampleCountFlags.Count1Bit,
            Tiling = ImageTiling.Optimal,
            Usage = ImageUsageFlags.TransferDstBit | ImageUsageFlags.SampledBit,
            SharingMode = SharingMode.Exclusive,
            InitialLayout = ImageLayout.Undefined
        };

        if (_vk.CreateImage(_vkInterface.Devices.LogicalDevice, in imageInfo, null, out Image textureImage) != Result.Success)
        {
            throw new Exception("Failed to create texture image.");
        }

        DeviceMemory imageMemory = VulkanUtilities.CreateMemory(_vk, _vkInterface.Devices, textureImage, MemoryPropertyFlags.DeviceLocalBit);

        // Transition and copy
        TransitionImageLayout(textureImage, ImageLayout.Undefined, ImageLayout.TransferDstOptimal);
        CopyBufferToImage(stagingBuffer, textureImage, (uint)img.Width, (uint)img.Height);
        TransitionImageLayout(textureImage, ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal);

        // Clean up staging buffer
        _vk.DestroyBuffer(_vkInterface.Devices.LogicalDevice, stagingBuffer, null);
        _vk.FreeMemory(_vkInterface.Devices.LogicalDevice, stagingMemory, null);

        // Create image view
        ImageView imageView = VulkanUtilities.CreateImageView(_vk, _vkInterface.Devices.LogicalDevice, textureImage, Format.R8G8B8A8Srgb);

        // Create sampler
        _vk.GetPhysicalDeviceProperties(_vkInterface.Devices.PhysicalDevice, out PhysicalDeviceProperties properties);

        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = properties.Limits.MaxSamplerAnisotropy,
            BorderColor = BorderColor.IntOpaqueBlack,
            UnnormalizedCoordinates = false,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            MipmapMode = SamplerMipmapMode.Linear
        };

        if (_vk.CreateSampler(_vkInterface.Devices.LogicalDevice, in samplerInfo, null, out Sampler sampler) != Result.Success)
        {
            throw new Exception("Failed to create texture sampler.");
        }

        return new TextureResources
        {
            Image = textureImage,
            ImageMemory = imageMemory,
            ImageView = imageView,
            Sampler = sampler
        };
    }

    private void TransitionImageLayout(Image image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        CommandBuffer cmd = VulkanUtilities.BeginSingleTimeCommands(_vk, _vkInterface.Devices, _vkInterface.Devices.GraphicsCommandPool);

        ImageMemoryBarrier barrier = new()
        {
            SType = StructureType.ImageMemoryBarrier,
            OldLayout = oldLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Vk.QueueFamilyIgnored,
            DstQueueFamilyIndex = Vk.QueueFamilyIgnored,
            Image = image,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        PipelineStageFlags sourceStage, destinationStage;

        if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.TransferDstOptimal)
        {
            barrier.SrcAccessMask = 0;
            barrier.DstAccessMask = AccessFlags.TransferWriteBit;
            sourceStage = PipelineStageFlags.TopOfPipeBit;
            destinationStage = PipelineStageFlags.TransferBit;
        }
        else if (oldLayout == ImageLayout.TransferDstOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
        {
            barrier.SrcAccessMask = AccessFlags.TransferWriteBit;
            barrier.DstAccessMask = AccessFlags.ShaderReadBit;
            sourceStage = PipelineStageFlags.TransferBit;
            destinationStage = PipelineStageFlags.FragmentShaderBit;
        }
        else
        {
            throw new Exception("Unsupported layout transition.");
        }

        _vk.CmdPipelineBarrier(cmd, sourceStage, destinationStage, 0, 0, null, 0, null, 1, in barrier);

        VulkanUtilities.EndSingleTimeCommands(_vk, _vkInterface.Devices, _vkInterface.Devices.GraphicsCommandPool, cmd);
    }

    private void CopyBufferToImage(Buffer buffer, Image image, uint width, uint height)
    {
        CommandBuffer cmd = VulkanUtilities.BeginSingleTimeCommands(_vk, _vkInterface.Devices, _vkInterface.Devices.GraphicsCommandPool);

        BufferImageCopy region = new()
        {
            BufferOffset = 0,
            BufferRowLength = 0,
            BufferImageHeight = 0,
            ImageOffset = new Offset3D(0, 0, 0),
            ImageExtent = new Extent3D(width, height, 1),
            ImageSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlags.ColorBit,
                MipLevel = 0,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        _vk.CmdCopyBufferToImage(cmd, buffer, image, ImageLayout.TransferDstOptimal, 1, in region);

        VulkanUtilities.EndSingleTimeCommands(_vk, _vkInterface.Devices, _vkInterface.Devices.GraphicsCommandPool, cmd);
    }

    private ShaderPipeline CreatePipeline(Shader? shader, Material? material)
    {
        // Use swapchain's render pass
        RenderPass renderPass = _vkInterface.SwapChain.RenderPass;

        // Determine shader filenames
        string vertShaderFilename = shader?.VertexShaderPath ?? "shader_base.vert.spv";
        string fragShaderFilename = shader?.FragmentShaderPath ?? "shader_base.frag.spv";

        // Load shader code (tries file system first, then embedded)
        byte[] vertShaderCode = GetShaderBytes(vertShaderFilename);
        byte[] fragShaderCode = GetShaderBytes(fragShaderFilename);

        ShaderModule vertShaderModule = CreateShaderModule(vertShaderCode);
        ShaderModule fragShaderModule = CreateShaderModule(fragShaderCode);

        try
        {
            // Determine if this shader uses textures based on material
            bool hasTexture = material?.AlbedoTexture != null;

            // Create descriptor set layout
            DescriptorSetLayout descriptorSetLayout = CreateDescriptorSetLayout(hasTexture);

            // Create pipeline layout
            PipelineLayout pipelineLayout = CreatePipelineLayout(descriptorSetLayout);

            // Create graphics pipeline
            Pipeline pipeline = CreateGraphicsPipeline(
                vertShaderModule,
                fragShaderModule,
                renderPass,
                pipelineLayout);

            return new ShaderPipeline
            {
                Pipeline = pipeline,
                PipelineLayout = pipelineLayout,
                DescriptorSetLayout = descriptorSetLayout
            };
        }
        finally
        {
            // Clean up shader modules
            _vk.DestroyShaderModule(_vkInterface.Devices.LogicalDevice, vertShaderModule, null);
            _vk.DestroyShaderModule(_vkInterface.Devices.LogicalDevice, fragShaderModule, null);
        }
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length
        };

        ShaderModule shaderModule;
        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            if (_vk.CreateShaderModule(_vkInterface.Devices.LogicalDevice, in createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception("Failed to create shader module.");
            }
        }

        return shaderModule;
    }

    private DescriptorSetLayout CreateDescriptorSetLayout(bool hasTexture)
    {
        // Binding 0: Uniform buffer (MVP matrices)
        DescriptorSetLayoutBinding uboBinding = new()
        {
            Binding = 0,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.UniformBuffer,
            StageFlags = ShaderStageFlags.VertexBit
        };

        // Binding 1: Combined image sampler (texture) - optional
        DescriptorSetLayoutBinding samplerBinding = new()
        {
            Binding = 1,
            DescriptorCount = 1,
            DescriptorType = DescriptorType.CombinedImageSampler,
            StageFlags = ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding* bindings = stackalloc DescriptorSetLayoutBinding[hasTexture ? 2 : 1];
        bindings[0] = uboBinding;
        if (hasTexture)
        {
            bindings[1] = samplerBinding;
        }

        DescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = hasTexture ? 2u : 1u,
            PBindings = bindings
        };

        if (_vk.CreateDescriptorSetLayout(_vkInterface.Devices.LogicalDevice, in layoutInfo, null, out DescriptorSetLayout layout) != Result.Success)
        {
            throw new Exception("Failed to create descriptor set layout.");
        }

        return layout;
    }

    private PipelineLayout CreatePipelineLayout(DescriptorSetLayout descriptorSetLayout)
    {
        DescriptorSetLayout* setLayouts = stackalloc DescriptorSetLayout[1] { descriptorSetLayout };

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 1,
            PSetLayouts = setLayouts
        };

        if (_vk.CreatePipelineLayout(_vkInterface.Devices.LogicalDevice, in pipelineLayoutInfo, null, out PipelineLayout pipelineLayout) != Result.Success)
        {
            throw new Exception("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }

    private Pipeline CreateGraphicsPipeline(ShaderModule vertModule, ShaderModule fragModule, RenderPass renderPass, PipelineLayout pipelineLayout)
    {
        // Shader stages
        PipelineShaderStageCreateInfo vertStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo fragStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo* shaderStages = stackalloc[] { vertStageInfo, fragStageInfo };

        // Vertex input
        VertexInputBindingDescription bindingDescription = VulkanVertex.GetBindingDescription();
        VertexInputAttributeDescription[] attributeDescriptions = VulkanVertex.GetAttributeDescriptions();

        fixed (VertexInputAttributeDescription* attributePtr = attributeDescriptions)
        {
            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                PVertexAttributeDescriptions = attributePtr
            };

            // Input assembly
            PipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false
            };

            // Viewport and scissor
            Extent2D extent = _vkInterface.SwapChain.Extent;
            Viewport viewport = new()
            {
                X = 0,
                Y = 0,
                Width = extent.Width,
                Height = extent.Height,
                MinDepth = 0,
                MaxDepth = 1
            };

            Rect2D scissor = new()
            {
                Offset = new Offset2D(0, 0),
                Extent = extent
            };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                PViewports = &viewport,
                ScissorCount = 1,
                PScissors = &scissor
            };

            // Rasterizer
            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false
            };

            // Multisampling
            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit
            };

            // Depth/stencil state
            PipelineDepthStencilStateCreateInfo depthStencil = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = false,  // Set to true if you want depth testing
                DepthWriteEnable = false,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false
            };

            // Color blending
            PipelineColorBlendAttachmentState colorBlendAttachment = new()
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                BlendEnable = false
            };

            PipelineColorBlendStateCreateInfo colorBlending = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment
            };

            // Create pipeline
            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                StageCount = 2,
                PStages = shaderStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PDepthStencilState = &depthStencil,
                PColorBlendState = &colorBlending,
                Layout = pipelineLayout,
                RenderPass = renderPass,
                Subpass = 0
            };

            if (_vk.CreateGraphicsPipelines(_vkInterface.Devices.LogicalDevice, default, 1, in pipelineInfo, null, out Pipeline pipeline) != Result.Success)
            {
                throw new Exception("Failed to create graphics pipeline.");
            }

            // Clean up
            SilkMarshal.Free((nint)vertStageInfo.PName);
            SilkMarshal.Free((nint)fragStageInfo.PName);

            return pipeline;
        }
    }

    private static byte[] GetShaderBytes(string filename)
    {
        // Strategy 1: Try loading from file system (development/debug scenarios)
        string baseDir = AppContext.BaseDirectory;
        string[] searchPaths =
        [
            Path.Combine(baseDir, "Shaders", filename),
        Path.Combine(baseDir, "Base", "Assets", "Shaders", filename),
        Path.Combine(baseDir, "Assets", "Shaders", filename),
        Path.Combine(baseDir, filename)
        ];

        foreach (string path in searchPaths)
        {
            if (File.Exists(path))
            {
                return File.ReadAllBytes(path);
            }
        }

        // Strategy 2: Try loading from embedded resources (production/packaged scenarios)
        Assembly assembly = Assembly.GetExecutingAssembly();
        string? resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(s => s.EndsWith(filename));

        if (resourceName != null)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using MemoryStream ms = new();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }

        // Strategy 3: Try loading from the calling assembly (for games embedding their own shaders)
        Assembly callingAssembly = Assembly.GetCallingAssembly();
        if (callingAssembly != assembly)
        {
            resourceName = callingAssembly.GetManifestResourceNames()
                .FirstOrDefault(s => s.EndsWith(filename));

            if (resourceName != null)
            {
                using Stream? stream = callingAssembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using MemoryStream ms = new();
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }

        throw new ApplicationException(
            $"Shader file '{filename}' not found. Searched:\n" +
            $"  - File paths: {string.Join(", ", searchPaths)}\n" +
            $"  - Embedded in: {assembly.FullName}\n" +
            $"  - Embedded in: {callingAssembly.FullName}");
    }

    private DescriptorPool CreateDescriptorPool()
    {
        DescriptorPoolSize* poolSizes = stackalloc DescriptorPoolSize[2];
        poolSizes[0] = new DescriptorPoolSize
        {
            Type = DescriptorType.UniformBuffer,
            DescriptorCount = MAX_DESCRIPTOR_SETS
        };
        poolSizes[1] = new DescriptorPoolSize
        {
            Type = DescriptorType.CombinedImageSampler,
            DescriptorCount = MAX_DESCRIPTOR_SETS
        };

        DescriptorPoolCreateInfo poolInfo = new()
        {
            SType = StructureType.DescriptorPoolCreateInfo,
            PoolSizeCount = 2,
            PPoolSizes = poolSizes,
            MaxSets = MAX_DESCRIPTOR_SETS
        };

        if (_vk.CreateDescriptorPool(_vkInterface.Devices.LogicalDevice, in poolInfo, null, out DescriptorPool pool) != Result.Success)
        {
            throw new Exception("Failed to create descriptor pool.");
        }

        return pool;
    }

    private DescriptorSet AllocateDescriptorSet(DescriptorSetLayout layout)
    {
        DescriptorSetAllocateInfo allocInfo = new()
        {
            SType = StructureType.DescriptorSetAllocateInfo,
            DescriptorPool = _descriptorPool,
            DescriptorSetCount = 1,
            PSetLayouts = &layout
        };

        if (_vk.AllocateDescriptorSets(_vkInterface.Devices.LogicalDevice, in allocInfo, out DescriptorSet descriptorSet) != Result.Success)
        {
            throw new Exception("Failed to allocate descriptor set.");
        }

        return descriptorSet;
    }

    private void UpdateUniformBuffer(ICamera camera, Rendering.Mesh mesh, MeshGpuResources resources)
    {
        UniformBufferObject ubo = new()
        {
            Model = mesh.Transform.GetModelMatrix(),
            View = camera.ViewMatrix,
            Projection = camera.ProjectionMatrix
        };

        // Flip Y for Vulkan coordinate system
        ubo.Projection.M22 *= -1;

        void* data;
        _vk.MapMemory(_vkInterface.Devices.LogicalDevice, resources.UniformMemory, 0,
            (ulong)Unsafe.SizeOf<UniformBufferObject>(), 0, &data);
        new Span<UniformBufferObject>(data, 1)[0] = ubo;
        _vk.UnmapMemory(_vkInterface.Devices.LogicalDevice, resources.UniformMemory);
    }

    private void BindDescriptorSet(CommandBuffer cmd, ShaderPipeline pipeline, MeshGpuResources resources, TextureResources? texture, int frameIndex)
    {
        // Update descriptor set with uniform buffer and texture
        DescriptorBufferInfo bufferInfo = new()
        {
            Buffer = resources.UniformBuffer,
            Offset = 0,
            Range = (ulong)Unsafe.SizeOf<UniformBufferObject>()
        };

        WriteDescriptorSet* writes = stackalloc WriteDescriptorSet[texture != null ? 2 : 1];

        writes[0] = new WriteDescriptorSet
        {
            SType = StructureType.WriteDescriptorSet,
            DstSet = resources.DescriptorSets[frameIndex], // USE FRAME INDEX
            DstBinding = 0,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo
        };

        int writeCount = 1;

        if (texture != null)
        {
            DescriptorImageInfo imageInfo = new()
            {
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageView = texture.ImageView,
                Sampler = texture.Sampler
            };

            writes[1] = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = resources.DescriptorSets[frameIndex], // USE FRAME INDEX
                DstBinding = 1,
                DstArrayElement = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                PImageInfo = &imageInfo
            };

            writeCount = 2;
        }

        _vk.UpdateDescriptorSets(_vkInterface.Devices.LogicalDevice, (uint)writeCount, writes, 0, null);

        // Bind descriptor set
        DescriptorSet descriptorSet = resources.DescriptorSets[frameIndex]; // USE FRAME INDEX
        _vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, pipeline.PipelineLayout, 0, 1,
            &descriptorSet, 0, null);
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        // Clean up mesh resources
        foreach (var resources in _meshResources.Values)
        {
            _vk.DestroyBuffer(_vkInterface.Devices.LogicalDevice, resources.VertexBuffer, null);
            _vk.FreeMemory(_vkInterface.Devices.LogicalDevice, resources.VertexMemory, null);
            _vk.DestroyBuffer(_vkInterface.Devices.LogicalDevice, resources.IndexBuffer, null);
            _vk.FreeMemory(_vkInterface.Devices.LogicalDevice, resources.IndexMemory, null);
            _vk.DestroyBuffer(_vkInterface.Devices.LogicalDevice, resources.UniformBuffer, null);
            _vk.FreeMemory(_vkInterface.Devices.LogicalDevice, resources.UniformMemory, null);
        }
        _meshResources.Clear();

        // Clean up pipelines
        foreach (var pipeline in _pipelines.Values)
        {
            _vk.DestroyPipeline(_vkInterface.Devices.LogicalDevice, pipeline.Pipeline, null);
            _vk.DestroyPipelineLayout(_vkInterface.Devices.LogicalDevice, pipeline.PipelineLayout, null);
            _vk.DestroyDescriptorSetLayout(_vkInterface.Devices.LogicalDevice, pipeline.DescriptorSetLayout, null);
        }
        _pipelines.Clear();

        // Clean up textures
        foreach (var texture in _textures.Values)
        {
            _vk.DestroySampler(_vkInterface.Devices.LogicalDevice, texture.Sampler, null);
            _vk.DestroyImageView(_vkInterface.Devices.LogicalDevice, texture.ImageView, null);
            _vk.DestroyImage(_vkInterface.Devices.LogicalDevice, texture.Image, null);
            _vk.FreeMemory(_vkInterface.Devices.LogicalDevice, texture.ImageMemory, null);
        }
        _textures.Clear();

        // Clean up descriptor pool
        _vk.DestroyDescriptorPool(_vkInterface.Devices.LogicalDevice, _descriptorPool, null);

        _isDisposed = true;
    }

    private sealed class MeshGpuResources
    {
        public required Buffer VertexBuffer { get; init; }
        public required DeviceMemory VertexMemory { get; init; }
        public required Buffer IndexBuffer { get; init; }
        public required DeviceMemory IndexMemory { get; init; }
        public required Buffer UniformBuffer { get; init; }
        public required DeviceMemory UniformMemory { get; init; }
        public DescriptorSet[] DescriptorSets { get; set; } = new DescriptorSet[VulkanSwapChainNew.MAX_FRAMES_IN_FLIGHT];
    }

    private sealed class ShaderPipeline
    {
        public required Pipeline Pipeline { get; init; }
        public required PipelineLayout PipelineLayout { get; init; }
        public required DescriptorSetLayout DescriptorSetLayout { get; init; }
    }

    private sealed class TextureResources
    {
        public required Image Image { get; init; }
        public required DeviceMemory ImageMemory { get; init; }
        public required ImageView ImageView { get; init; }
        public required Sampler Sampler { get; init; }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct VulkanVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 Color;
    public Vector2 TexCoord;

    public static VertexInputBindingDescription GetBindingDescription()
    {
        return new VertexInputBindingDescription
        {
            Binding = 0,
            Stride = (uint)Unsafe.SizeOf<VulkanVertex>(),
            InputRate = VertexInputRate.Vertex
        };
    }

    public static VertexInputAttributeDescription[] GetAttributeDescriptions()
    {
        return new[]
        {
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 0,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)Marshal.OffsetOf<VulkanVertex>(nameof(Position))
            },
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 1,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)Marshal.OffsetOf<VulkanVertex>(nameof(Normal))
            },
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 2,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)Marshal.OffsetOf<VulkanVertex>(nameof(Color))
            },
            new VertexInputAttributeDescription
            {
                Binding = 0,
                Location = 3,
                Format = Format.R32G32Sfloat,
                Offset = (uint)Marshal.OffsetOf<VulkanVertex>(nameof(TexCoord))
            }
        };
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct UniformBufferObject
{
    public Matrix4x4 Model;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
}