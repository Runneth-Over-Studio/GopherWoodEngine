using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.GraphicsDeviceInterface.VulkanBackend;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules;

internal abstract class VulkanPipeline : IDisposable
{
    private readonly VulkanGraphicsDeviceInterface _vkInterface;
    private readonly IDictionary<uint, DescriptorSetLayout> _setLayouts = new Dictionary<uint, DescriptorSetLayout>();
    private readonly IList<ShaderModule> _shaderModules = [];
    private readonly IList<PipelineShaderStageCreateInfo> _shaderStages = [];
    private readonly RenderPass _renderPass;
    private readonly Pipeline _pipeline;
    private readonly PipelineLayout _pipelineLayout;

    protected VulkanPipeline(RenderPass renderPass)
    {
        _vkInterface = Ioc.Default.GetRequiredService<VulkanGraphicsDeviceInterface>();
        _renderPass = renderPass;

        LoadShaderModules();

        RegisterDescriptors();

        _pipelineLayout = CreatePipelineLayout();
        _pipeline = CreatePipeline();
    }

    protected abstract void LoadShaderModules();

    protected abstract VertexInputAttributeDescription[] GetVertexDescriptions(out VertexInputBindingDescription? bindingDescription);

    protected unsafe abstract void RegisterDescriptors();

    protected virtual PipelineColorBlendAttachmentState ColourBlendAttachment()
    {
        return new PipelineColorBlendAttachmentState()
        {
            ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
            BlendEnable = false,
            SrcColorBlendFactor = BlendFactor.One,
            DstColorBlendFactor = BlendFactor.Zero,
            ColorBlendOp = BlendOp.Add,
            SrcAlphaBlendFactor = BlendFactor.One,
            DstAlphaBlendFactor = BlendFactor.Zero,
            AlphaBlendOp = BlendOp.Add
        };
    }

    protected virtual PrimitiveTopology Topology => PrimitiveTopology.TriangleList;

    protected virtual PolygonMode PolygonMode => PolygonMode.Fill;

    protected virtual CullModeFlags CullMode => CullModeFlags.BackBit;

    protected virtual FrontFace FrontFace => FrontFace.CounterClockwise;

    protected virtual bool DepthTest => true;

    protected virtual bool StencilTest => false;

    protected virtual PipelineTessellationStateCreateInfo? TessellationState => null;

    protected virtual PushConstantRange[] GetPushConstantRanges() { return []; }

    protected unsafe void LoadShaderModule(string shaderFile, ShaderStageFlags stage)
    {
        string shaderName = Path.GetFileName(shaderFile);
        byte[] shaderCode = File.ReadAllBytes(shaderFile);

        LoadShaderModule(shaderName, shaderCode, stage);
    }

    protected unsafe void LoadShaderModule(string shaderName, byte[] shaderCode, ShaderStageFlags stage)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (uint)shaderCode.Length
        };

        fixed (byte* ptr = shaderCode)
        {
            createInfo.PCode = (uint*)ptr;
        }

        if (_vkInterface.VulkanAPI.Vk.CreateShaderModule(_vkInterface.Devices.LogicalDevice, in createInfo, null, out ShaderModule shaderModule) != Result.Success)
        {
            throw new InvalidOperationException($"Failed to create ShaderModule \"{shaderName}\".");
        }
        _shaderModules.Add(shaderModule);

        _shaderStages.Add(new PipelineShaderStageCreateInfo()
        {
            Stage = stage,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        });
    }

    // Read shader modules (SPV files) that were built and embedded into the application.
    protected static byte[] GetEmbeddedShaderBytes(string filename)
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

    private DescriptorSetLayout[] GetSortedSetLayouts()
    {
        uint[] desriptorSetLayoutIndices = [.. _setLayouts.Keys];
        Array.Sort(desriptorSetLayoutIndices);

        DescriptorSetLayout[] descriptorSetLayouts = new DescriptorSetLayout[desriptorSetLayoutIndices.Length];
        for (int i = 0; i < desriptorSetLayoutIndices.Length; i++)
        {
            descriptorSetLayouts[i] = _setLayouts[desriptorSetLayoutIndices[i]];
        }

        return descriptorSetLayouts;
    }

    private unsafe PipelineLayout CreatePipelineLayout()
    {
        PushConstantRange[] pushConstantRanges = GetPushConstantRanges();

        PipelineLayoutCreateInfo createInfo = new();
        fixed (PushConstantRange* ptr = pushConstantRanges)
        {
            createInfo.PushConstantRangeCount = (uint)pushConstantRanges.Length;
            createInfo.PPushConstantRanges = ptr;
        }

        DescriptorSetLayout[] descriptorSetLayouts = GetSortedSetLayouts();
        fixed (DescriptorSetLayout* ptr = descriptorSetLayouts)
        {
            createInfo.SetLayoutCount = (uint)descriptorSetLayouts.Length;
            createInfo.PSetLayouts = ptr;
        }

        if (_vkInterface.VulkanAPI.Vk.CreatePipelineLayout(_vkInterface.Devices.LogicalDevice, in createInfo, null, out PipelineLayout pipelineLayout) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create pipeline layout.");
        }

        return pipelineLayout;
    }

    protected unsafe void RegisterDescriptor(uint set, bool pushDescriptor = false, params DescriptorSetLayoutBinding[] bindings)
    {
        if (_setLayouts.ContainsKey(set))
        {
            throw new InvalidOperationException($"DescriptorSetLayout 'set={set}' already specified for this pipeline.");
        }

        DescriptorSetLayoutCreateInfo createInfo = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            Flags = pushDescriptor ? DescriptorSetLayoutCreateFlags.PushDescriptorBitKhr : 0,
            BindingCount = (uint)bindings.Length
        };

        fixed (DescriptorSetLayoutBinding* ptr = bindings)
        {
            createInfo.PBindings = ptr;
        }

        if (_vkInterface.VulkanAPI.Vk.CreateDescriptorSetLayout(_vkInterface.Devices.LogicalDevice, in createInfo, null, out DescriptorSetLayout descriptorSetLayout) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create descriptor set layout.");
        }

        _setLayouts.Add(set, descriptorSetLayout);
    }

    private unsafe Pipeline CreatePipeline()
    {
        DynamicState* dynamicStates = stackalloc DynamicState[2]
        {
            DynamicState.Viewport,
            DynamicState.Scissor
        };

        PipelineDynamicStateCreateInfo dynamicStateInfo = new()
        {
            SType = StructureType.PipelineDynamicStateCreateInfo,
            DynamicStateCount = 2,
            PDynamicStates = dynamicStates
        };

        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexAttributeDescriptionCount = 0
        };

        VertexInputAttributeDescription[] attributeDescriptions = GetVertexDescriptions(out VertexInputBindingDescription? bindingDescription);
        VertexInputBindingDescription bDescription;
        if (bindingDescription.HasValue)
        {
            bDescription = bindingDescription.Value;
            vertexInputInfo.VertexBindingDescriptionCount = 1;
            vertexInputInfo.PVertexBindingDescriptions = &bDescription;
            fixed (VertexInputAttributeDescription* ptr = attributeDescriptions)
            {
                vertexInputInfo.VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length;
                vertexInputInfo.PVertexAttributeDescriptions = ptr;
            }
        }

        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            Topology = Topology,
            PrimitiveRestartEnable = false
        };

        PipelineViewportStateCreateInfo viewportState = new()
        {
            ViewportCount = 1,
            ScissorCount = 1
        };

        PipelineRasterizationStateCreateInfo rasteriser = new()
        {
            DepthClampEnable = false,
            RasterizerDiscardEnable = false,
            PolygonMode = PolygonMode,
            LineWidth = 1.0f,
            CullMode = CullMode,
            FrontFace = FrontFace,
            DepthBiasEnable = false,
            DepthBiasConstantFactor = 0.0f,
            DepthBiasClamp = 0.0f,
            DepthBiasSlopeFactor = 0.0f
        };

        PipelineMultisampleStateCreateInfo multisampling = new()
        {
            SampleShadingEnable = false,
            RasterizationSamples = SampleCountFlags.Count1Bit,
            MinSampleShading = 1.0f,
            PSampleMask = null,
            AlphaToCoverageEnable = false,
            AlphaToOneEnable = false
        };

        PipelineColorBlendAttachmentState colourBlendAttachment = ColourBlendAttachment();

        PipelineColorBlendStateCreateInfo colourBlending = new()
        {
            LogicOpEnable = false,
            LogicOp = LogicOp.Copy,
            AttachmentCount = 1,
            PAttachments = &colourBlendAttachment
        };

        PipelineDepthStencilStateCreateInfo depthStencil = new()
        {
            DepthTestEnable = DepthTest,
            DepthWriteEnable = DepthTest,
            DepthCompareOp = CompareOp.Less,
            DepthBoundsTestEnable = false,
            MinDepthBounds = 0.0f,
            MaxDepthBounds = 1.0f,
            StencilTestEnable = StencilTest
        };

        PipelineTessellationStateCreateInfo? tesselationState = TessellationState;
        PipelineTessellationStateCreateInfo tessPtrSrc = tesselationState ?? default;

        GraphicsPipelineCreateInfo pipelineCreateInfo = new()
        {
            SType = StructureType.GraphicsPipelineCreateInfo,
            PVertexInputState = &vertexInputInfo,
            PInputAssemblyState = &inputAssembly,
            PViewportState = &viewportState,
            PRasterizationState = &rasteriser,
            PMultisampleState = &multisampling,
            PDepthStencilState = (DepthTest || StencilTest) ? &depthStencil : null,
            PColorBlendState = &colourBlending,
            PDynamicState = &dynamicStateInfo,
            PTessellationState = tesselationState.HasValue ? &tessPtrSrc : null,
            Layout = _pipelineLayout,
            RenderPass = _renderPass,
            Subpass = 0,
            BasePipelineIndex = -1
        };

        PipelineShaderStageCreateInfo[] shaderStages = [.. _shaderStages];
        fixed (PipelineShaderStageCreateInfo* ptr = shaderStages)
        {
            pipelineCreateInfo.StageCount = (uint)shaderStages.Length;
            pipelineCreateInfo.PStages = ptr;
        }

        if (_vkInterface.VulkanAPI.Vk.CreateGraphicsPipelines(_vkInterface.Devices.LogicalDevice, new PipelineCache(handle: null), 1, in pipelineCreateInfo, null, out Pipeline pipeline) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create graphics pipeline.");
        }

        return pipeline;
    }

    public DescriptorSetLayout GetSetLayout(uint setIndex)
    {
        return _setLayouts[setIndex];
    }

    public void Bind(CommandBuffer cmd)
    {
        _vkInterface.VulkanAPI.Vk.CmdBindPipeline(cmd, PipelineBindPoint.Graphics, _pipeline);
    }

    public unsafe void PushConstants<T>(ShaderStageFlags shader, T constants, CommandBuffer cmd) where T : unmanaged
    {
        _vkInterface.VulkanAPI.Vk.CmdPushConstants(cmd, _pipelineLayout, shader, 0, (uint)Marshal.SizeOf<T>(), &constants);
    }

    public unsafe void BindDescriptorSet(uint set, DescriptorSet descriptor, CommandBuffer cmd)
    {
        _vkInterface.VulkanAPI.Vk.CmdBindDescriptorSets(cmd, PipelineBindPoint.Graphics, _pipelineLayout, set, 1, in descriptor, 0, null);
    }

    public unsafe void UpdateDescriptorSet<T>(uint set, uint binding, VulkanUniformValue<T> value, CommandBuffer cmd) where T : unmanaged
    {
        DescriptorBufferInfo bufferInfo = value.BufferInfo();

        WriteDescriptorSet descriptorWrite = new()
        {
            DstBinding = binding,
            DstArrayElement = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            PBufferInfo = &bufferInfo,
            PImageInfo = null,
            PTexelBufferView = null
        };

        _vkInterface.SwapChain.PushDescriptor.CmdPushDescriptorSet(cmd, PipelineBindPoint.Graphics, _pipelineLayout, set, 1, in descriptorWrite);
    }

    public unsafe void Dispose()
    {
        _vkInterface.VulkanAPI.Vk.DestroyPipeline(_vkInterface.Devices.LogicalDevice, _pipeline, null);

        foreach (DescriptorSetLayout descriptorLayout in _setLayouts.Values)
        {
            _vkInterface.VulkanAPI.Vk.DestroyDescriptorSetLayout(_vkInterface.Devices.LogicalDevice, descriptorLayout, null);
        }
        _vkInterface.VulkanAPI.Vk.DestroyPipelineLayout(_vkInterface.Devices.LogicalDevice, _pipelineLayout, null);

        foreach (ShaderModule shaderModule in _shaderModules)
        {
            _vkInterface.VulkanAPI.Vk.DestroyShaderModule(_vkInterface.Devices.LogicalDevice, shaderModule, null);
        }
    }
}
