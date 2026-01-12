using Silk.NET.Vulkan;
using System.Collections.Generic;

namespace GopherWoodEngine.Runtime.Modules;

internal class ModelRenderer : ISubRenderer
{
    public int RenderOrder => 100;

    private readonly IDictionary<Model, IList<Transform>> _instances = new Dictionary<Model, IList<Transform>>();
    private readonly Vk _vk;

    public ModelPipeline Pipeline { get; }

    public ModelRenderer()
    {
        VulkanGraphicsDeviceInterface _vkInterface = Ioc.Default.GetRequiredService<VulkanGraphicsDeviceInterface>();

        _vk = _vkInterface.VulkanAPI.Vk;

        Pipeline = new ModelPipeline(_vkInterface.SwapChain.RenderPass);
    }

    public void RenderModel(Model model, Transform transform)
    {
        if (!_instances.TryGetValue(model, out IList<Transform>? transforms))
        {
            transforms = [];
            _instances.Add(model, transforms);
        }

        transforms.Add(transform);
    }

    public void Render(ICamera camera, CommandBuffer cmd)
    {
        Pipeline.Bind(cmd);

        foreach (Model model in _instances.Keys)
        {
            Buffer handle = model.Mesh.VertexBuffer.Handle;
            ulong pOffsets = 0;
            _vk.CmdBindVertexBuffers(cmd, 0, 1, ref handle, ref pOffsets);
            _vk.CmdBindIndexBuffer(cmd, model.Mesh.IndexBuffer.Handle, 0, IndexType.Uint16);
            Pipeline.BindDescriptorSet(0, model.Texture, cmd);
            foreach (Transform transform in _instances[model])
            {
                var pushConstants = new ModelPushConstant(transform.TransformationMatrix, camera.ViewProjection);
                Pipeline.PushConstants(ShaderStageFlags.VertexBit, pushConstants, cmd);
                _vk.CmdDrawIndexed(cmd, model.Mesh.IndexBuffer.Count, 1, 0, 0, 0);
            }
        }

        _instances.Clear();
    }

    public void Dispose()
    {
        Pipeline.Dispose();
    }
}
