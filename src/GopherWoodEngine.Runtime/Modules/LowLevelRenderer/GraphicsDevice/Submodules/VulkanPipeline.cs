using Silk.NET.Core.Native;
using Silk.NET.OpenAL;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe class VulkanPipeline : IDisposable
{
    private readonly RenderPass _renderPass;
    private readonly PipelineLayout _pipelineLayout;
    private readonly VulkanSwapChain _swapChain;
    private readonly Vk _vk;
    private readonly Device _logicalDevice;
    private bool _disposed = false;

    public VulkanPipeline(Vk vk, Device logicalDevice, VulkanSwapChain swapChain)
    {
        _vk = vk;
        _logicalDevice = logicalDevice;
        _swapChain = swapChain;
        _renderPass = CreateRenderPass(vk, logicalDevice, swapChain.ImageFormat);
        _pipelineLayout = CreatePipelineLayout(vk, logicalDevice);
    }

    /// <summary>
    /// The graphics pipeline is the sequence of operations that take the vertices and textures of your meshes all the way to the pixels in the render targets.
    /// </summary>
    internal void CreateGraphicsPipeline()
    {
        byte[] vertShaderCode = GetEmbeddedShaderBytes("09_shader_base.vert.spv");
        byte[] fragShaderCode = GetEmbeddedShaderBytes("09_shader_base.frag.spv");

        ShaderModule vertShaderModule = CreateShaderModule(vertShaderCode);
        ShaderModule fragShaderModule = CreateShaderModule(fragShaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = vertShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = fragShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        PipelineShaderStageCreateInfo* shaderStages = stackalloc[]
        {
            vertShaderStageInfo,
            fragShaderStageInfo
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0
        };

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false
        };

        Viewport viewport = new()
        {
            X = 0,
            Y = 0,
            Width = _swapChain.Extent.Width,
            Height = _swapChain.Extent.Height,
            MinDepth = 0,
            MaxDepth = 1
        };

        Rect2D scissor = new()
        {
            Offset = { X = 0, Y = 0 },
            Extent = _swapChain.Extent
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            SType = StructureType.PipelineViewportStateCreateInfo,
            ViewportCount = 1,
            PViewports = &viewport,
            ScissorCount = 1,
            PScissors = &scissor
        };

        PipelineRasterizationStateCreateInfo rasterizer = new()
        {
            SType = StructureType.PipelineRasterizationStateCreateInfo,
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode.Fill,
            LineWidth = 1,
            CullMode = CullModeFlags.BackBit,
            FrontFace = FrontFace.Clockwise,
            DepthBiasEnable = false
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SType = StructureType.PipelineMultisampleStateCreateInfo,
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit
        };

        PipelineColorBlendAttachmentState colorBlendAttachment = new()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false
        };

        PipelineColorBlendStateCreateInfo colorBlending = new()
        {
            SType = StructureType.PipelineColorBlendStateCreateInfo,
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colorBlendAttachment
        };

        colorBlending.BlendConstants[0] = 0;
        colorBlending.BlendConstants[1] = 0;
        colorBlending.BlendConstants[2] = 0;
        colorBlending.BlendConstants[3] = 0;

        _vk.DestroyShaderModule(_logicalDevice, fragShaderModule, null);
        _vk.DestroyShaderModule(_logicalDevice, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
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

            if (_vk.CreateShaderModule(_logicalDevice, in createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception("Creation of shader module was unsuccessful.");
            }
        }

        return shaderModule;
    }

    private static RenderPass CreateRenderPass(Vk vk, Device logicalDevice, Format swapChainImageFormat)
    {
        AttachmentDescription colorAttachment = new()
        {
            Format = swapChainImageFormat,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef
        };

        RenderPassCreateInfo renderPassInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = 1,
            PAttachments = &colorAttachment,
            SubpassCount = 1,
            PSubpasses = &subpass
        };

        if (vk.CreateRenderPass(logicalDevice, in renderPassInfo, null, out RenderPass renderPass) != Result.Success)
        {
            throw new Exception("Failed to create render pass.");
        }

        return renderPass;
    }

    private static PipelineLayout CreatePipelineLayout(Vk vk, Device logicalDevice)
    {
        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            SetLayoutCount = 0,
            PushConstantRangeCount = 0
        };

        if (vk.CreatePipelineLayout(logicalDevice, in pipelineLayoutInfo, null, out PipelineLayout pipelineLayout) != Result.Success)
        {
            throw new Exception("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }

    /// <summary>
    /// Read shader modules (SPV files) that were built and stored locally relative to the application.
    /// </summary>
    private static byte[] GetLocalShaderBytes(string filePath)
    {
        return File.ReadAllBytes(filePath);
    }

    /// <summary>
    /// Read shader modules (SPV files) that were built and embedded into the application.
    /// </summary>
    private static byte[] GetEmbeddedShaderBytes(string filename)
    {
        //TODO: Right now have to build twice. First to build the shaders, then again for them to actually be available.
        //  But going to change how shaders are built in the future, so this is fine for now.

        Assembly assembly = Assembly.GetExecutingAssembly();

        string? resourceName = assembly.GetManifestResourceNames().FirstOrDefault(s => s.EndsWith(filename))
            ?? throw new ApplicationException($"No shader file found with name {filename}. Did you forget to set glsl file to Embedded Resource/Do Not Copy?");

        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new ApplicationException($"No shader file found at {resourceName}. Did you forget to set glsl file to Embedded Resource/Do Not Copy?");

        using MemoryStream ms = new();
        stream.CopyTo(ms);

        return ms.ToArray();
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
                _vk.DestroyPipelineLayout(_logicalDevice, _pipelineLayout, null);
                _vk.DestroyRenderPass(_logicalDevice, _renderPass, null);
            }

            _disposed = true;
        }
    }
}
