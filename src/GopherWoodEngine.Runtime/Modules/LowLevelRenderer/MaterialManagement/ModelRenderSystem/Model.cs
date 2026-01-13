using GopherWoodEngine.Runtime.Modules.LowLevelRenderer.MaterialManagement.ModelRenderSystem;
using Silk.NET.Vulkan;
using System;

namespace GopherWoodEngine.Runtime.Modules;

internal class Model : IDisposable
{
    public Mesh Mesh { get; }

    public DescriptorSet Texture { get; }

    public Model(Mesh mesh, DescriptorSet texture)
    {
        Mesh = mesh;
        Texture = texture;
    }

    public void Dispose()
    {
        Mesh.Dispose();
    }
}
