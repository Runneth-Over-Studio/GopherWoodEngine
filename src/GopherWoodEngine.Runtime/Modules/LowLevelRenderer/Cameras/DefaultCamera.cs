using Silk.NET.Windowing;
using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules;

internal class DefaultCamera : ICamera
{
    public Vector3 Facing { get; set; } = new Vector3(0.0f, 0.0f, -1.0f);

    public Vector3 Position { get; set; } = new Vector3(0.0f, 0.0f, 0.0f);

    public Matrix4x4 ViewMatrix { get; set; } = default;

    public Matrix4x4 ProjectionMatrix { get; set; } = default;

    public Matrix4x4 ViewProjection { get; set; } = default;

    private readonly IWindow _window;
    private readonly float _fieldOfView;
    private readonly float _nearPlane;
    private readonly float _farPlane;

    public DefaultCamera(IWindow window, float fieldOfView, float nearPlane, float farPlane)
    {
        _window = window;
        _fieldOfView = fieldOfView;
        _nearPlane = nearPlane;
        _farPlane = farPlane;
    }

    public void Update()
    {
        ViewMatrix = Matrix4x4.CreateLookAt(Position, Position + Vector3.Normalize(Facing), Vector3.UnitY);

        Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(_fieldOfView, _window.AspectRatio(), _nearPlane, _farPlane);
        projection.M22 *= -1;
        ProjectionMatrix = projection;

        ViewProjection = ViewMatrix * ProjectionMatrix;
    }
}
