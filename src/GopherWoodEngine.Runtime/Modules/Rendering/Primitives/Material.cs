using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a material that defines the visual appearance of a surface.
/// </summary>
public sealed class Material
{
    public string Name { get; set; } = "DefaultMaterial";
    public Shader? Shader { get; set; }
    public Texture? AlbedoTexture { get; set; }
    public Vector4 AlbedoColor { get; set; } = Vector4.One;
    public float Metallic { get; set; } = 0.0f;
    public float Roughness { get; set; } = 0.5f;
}
