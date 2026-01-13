using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a vertex in 3D space with position, normal, texture coordinates, and color.
/// </summary>
public record struct Vertex
{
    public Vector3 Position { get; init; }
    public Vector3 Normal { get; init; }
    public Vector2 TexCoord { get; init; }
    public Vector3 Color { get; init; }
}
