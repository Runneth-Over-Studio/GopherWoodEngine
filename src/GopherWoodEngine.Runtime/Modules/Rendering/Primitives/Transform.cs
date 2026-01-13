using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents the position, rotation, and scale of a game object in 3D space.
/// </summary>
public record Transform
{
    public Vector3 Position { get; init; } = Vector3.Zero;
    public Quaternion Rotation { get; init; } = Quaternion.Identity;
    public Vector3 Scale { get; init; } = Vector3.One;

    public Matrix4x4 GetModelMatrix()
    {
        return Matrix4x4.CreateScale(Scale) *
               Matrix4x4.CreateFromQuaternion(Rotation) *
               Matrix4x4.CreateTranslation(Position);
    }
}