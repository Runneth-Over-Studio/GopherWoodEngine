using System;
using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a 2D sprite for rendering.
/// </summary>
public sealed class Sprite : IRenderable
{
    public Guid Id { get; } = Guid.NewGuid();
    public Transform Transform { get; set; } = new();
    public bool IsVisible { get; set; } = true;
    public int RenderLayer { get; set; } = 200; // Sprites after opaque geometry

    public Texture? Texture { get; set; }
    public Vector4 TintColor { get; set; } = Vector4.One;
    public Rectangle SourceRectangle { get; set; }
}

public record struct Rectangle(float X, float Y, float Width, float Height);
