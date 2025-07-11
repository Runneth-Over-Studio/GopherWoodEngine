using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Device = Silk.NET.Vulkan.Device;

namespace GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDevice.Submodules;

internal unsafe sealed class VulkanPipeline : IDisposable
{
    private const string COMPILED_VERT_SHADER_NAME = "shader_base.vert.spv";
    private const string COMPILED_FRAG_SHADER_NAME = "shader_base.frag.spv";

    // Specifies how many color and depth buffers there will be, how many samples to use for each of them 
    // and how their contents should be handled throughout the rendering operations.
    internal RenderPass RenderPass { get; private set; }

    internal DescriptorSetLayout DescriptorSetLayout { get; }

    internal PipelineLayout PipelineLayout { get; private set; }

    internal Pipeline GraphicsPipeline { get; private set; }

    private readonly Vk _vk;
    private readonly Device _logicalDevice;
    private readonly VulkanSwapChain _swapChain;
    private bool _disposed = false;

    public VulkanPipeline(Vk vk, Device logicalDevice, VulkanSwapChain swapChain, DescriptorSetLayout descriptorSetLayout)
    {
        _vk = vk;
        _logicalDevice = logicalDevice;
        _swapChain = swapChain;
        RenderPass = CreateRenderPass(_vk, _logicalDevice, swapChain.ImageFormat);
        DescriptorSetLayout = descriptorSetLayout;
        PipelineLayout = CreatePipelineLayout(_vk, _logicalDevice, descriptorSetLayout);

        GraphicsPipeline = CreateGraphicsPipeline(
            _vk,
            _logicalDevice,
            swapChain.Extent,
            RenderPass,
            PipelineLayout,
            COMPILED_VERT_SHADER_NAME,
            COMPILED_FRAG_SHADER_NAME);
    }

    internal void CleanUpSwapChain()
    {
        _vk.DestroyPipeline(_logicalDevice, GraphicsPipeline, null);
        _vk.DestroyPipelineLayout(_logicalDevice, PipelineLayout, null);
        _vk.DestroyRenderPass(_logicalDevice, RenderPass, null);
    }

    internal void ResetSwapChain(VulkanSwapChain swapChain)
    {
        RenderPass = CreateRenderPass(_vk, _logicalDevice, swapChain.ImageFormat);
        PipelineLayout = CreatePipelineLayout(_vk, _logicalDevice, DescriptorSetLayout);

        GraphicsPipeline = CreateGraphicsPipeline(
            _vk,
            _logicalDevice,
            swapChain.Extent,
            RenderPass,
            PipelineLayout,
            COMPILED_VERT_SHADER_NAME,
            COMPILED_FRAG_SHADER_NAME);
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

    private static PipelineLayout CreatePipelineLayout(Vk vk, Device logicalDevice, DescriptorSetLayout descriptorSetLayout)
    {
        DescriptorSetLayout* setLayouts = stackalloc DescriptorSetLayout[1];
        setLayouts[0] = descriptorSetLayout;

        PipelineLayoutCreateInfo pipelineLayoutInfo = new()
        {
            SType = StructureType.PipelineLayoutCreateInfo,
            PushConstantRangeCount = 0,
            SetLayoutCount = 1,
            PSetLayouts = setLayouts
        };

        if (vk.CreatePipelineLayout(logicalDevice, in pipelineLayoutInfo, null, out PipelineLayout pipelineLayout) != Result.Success)
        {
            throw new Exception("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }

    private static Pipeline CreateGraphicsPipeline(Vk vk, Device logicalDevice, Extent2D swapChainExtent, RenderPass renderPass, PipelineLayout pipelineLayout, string vertShaderFilename, string fragShaderFilename)
    {
        Pipeline graphicsPipeline;

        byte[] vertShaderCode = GetEmbeddedShaderBytes(vertShaderFilename);
        byte[] fragShaderCode = GetEmbeddedShaderBytes(fragShaderFilename);

        ShaderModule vertShaderModule = CreateShaderModule(vk, logicalDevice, vertShaderCode);
        ShaderModule fragShaderModule = CreateShaderModule(vk, logicalDevice, fragShaderCode);

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

        VertexInputBindingDescription bindingDescription = Vertex.GetBindingDescription();
        VertexInputAttributeDescription[] attributeDescriptions = Vertex.GetAttributeDescriptions();

        fixed (VertexInputAttributeDescription* attributeDescriptionsPtr = attributeDescriptions)
        {

            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                PVertexBindingDescriptions = &bindingDescription,
                PVertexAttributeDescriptions = attributeDescriptionsPtr
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
                Width = swapChainExtent.Width,
                Height = swapChainExtent.Height,
                MinDepth = 0,
                MaxDepth = 1
            };

            Rect2D scissor = new()
            {
                Offset = { X = 0, Y = 0 },
                Extent = swapChainExtent
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
                FrontFace = FrontFace.CounterClockwise,
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
                PColorBlendState = &colorBlending,
                Layout = pipelineLayout,
                RenderPass = renderPass,
                Subpass = 0,
                BasePipelineHandle = default
            };

            if (vk.CreateGraphicsPipelines(logicalDevice, default, 1, in pipelineInfo, null, out graphicsPipeline) != Result.Success)
            {
                throw new Exception("Failed to create graphics pipeline.");
            }
        }

        vk.DestroyShaderModule(logicalDevice, fragShaderModule, null);
        vk.DestroyShaderModule(logicalDevice, vertShaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);

        return graphicsPipeline;
    }

    private static ShaderModule CreateShaderModule(Vk vk, Device logicalDevice, byte[] code)
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

            if (vk.CreateShaderModule(logicalDevice, in createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception("Creation of shader module was unsuccessful.");
            }
        }

        return shaderModule;
    }

    // Read shader modules (SPV files) that were built and stored locally relative to the application.
    private static byte[] GetLocalShaderBytes(string filePath)
    {
        return File.ReadAllBytes(filePath);
    }

    // Read shader modules (SPV files) that were built and embedded into the application.
    private static byte[] GetEmbeddedShaderBytes(string filename)
    {
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

    internal void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _vk.DestroyPipeline(_logicalDevice, GraphicsPipeline, null);
                _vk.DestroyPipelineLayout(_logicalDevice, PipelineLayout, null);
                //_vk.DestroyDescriptorSetLayout(_logicalDevice, DescriptorSetLayout, null);
                _vk.DestroyRenderPass(_logicalDevice, RenderPass, null);
            }

            _disposed = true;
        }
    }
}
