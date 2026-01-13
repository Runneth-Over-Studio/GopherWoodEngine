using System;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a 3D mesh with vertices, indices, and material.
/// </summary>
public sealed class Mesh : IRenderable
{
    public Guid Id { get; } = Guid.NewGuid();
    public Transform Transform { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public int RenderLayer { get; set; } = 100; // Opaque geometry

    public required Vertex[] Vertices { get; init; }
    public required uint[] Indices { get; init; }
    public Material? Material { get; set; }
}
