using Silk.NET.Vulkan;
using System.Runtime.InteropServices;

namespace GopherWoodEngine.Runtime.Modules;

internal class ModelPipeline(RenderPass renderPass) : VulkanPipeline(renderPass)
{
    protected override VertexInputAttributeDescription[] GetVertexDescriptions(out VertexInputBindingDescription? bindingDescription)
    {
        bindingDescription = new VertexInputBindingDescription()
        {
            Binding = 0,
            Stride = (uint)Marshal.SizeOf<MeshVertex>(),
            InputRate = VertexInputRate.Vertex
        };

        return
        [
            new VertexInputAttributeDescription()
            {
                Location = 0,
                Binding = 0,
                Format = Format.R32G32B32Sfloat,
                Offset = (uint)Marshal.OffsetOf<MeshVertex>("Position")
            },
            new VertexInputAttributeDescription()
            {
                Location = 1,
                Binding = 0,
                Format = Format.R32G32Sfloat,
                Offset = (uint)Marshal.OffsetOf<MeshVertex>("TextureCoordinates")
            }
        ];
    }

    protected override void LoadShaderModules()
    {
        LoadShaderModule("shader_model.vert.spv", GetEmbeddedShaderBytes("shader_model.vert.spv"), ShaderStageFlags.VertexBit);
        LoadShaderModule("shader_model.frag.spv", GetEmbeddedShaderBytes("shader_model.frag.spv"), ShaderStageFlags.FragmentBit);
    }

    protected override PushConstantRange[] GetPushConstantRanges()
    {
        return
        [
            new PushConstantRange()
            {
                StageFlags = ShaderStageFlags.VertexBit,
                Offset = 0,
                Size = (uint)Marshal.SizeOf<ModelPushConstant>()
            }
        ];
    }

    protected override unsafe void RegisterDescriptors()
    {
        RegisterDescriptor(
            0,
            false,
            new DescriptorSetLayoutBinding()
            {
                Binding = 0,
                DescriptorType = DescriptorType.CombinedImageSampler,
                DescriptorCount = 1,
                StageFlags = ShaderStageFlags.FragmentBit,
                PImmutableSamplers = null
            }
        );
    }
}
