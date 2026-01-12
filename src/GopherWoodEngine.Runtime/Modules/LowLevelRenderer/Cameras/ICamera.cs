using System.Numerics;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines the contract for a camera that provides view and projection transformations for rendering.
/// </summary>
/// <remarks>
/// <para>
/// Cameras are responsible for defining how the 3D scene is viewed and projected onto the 2D screen.
/// They provide the view matrix (camera position and orientation) and projection matrix (perspective
/// or orthographic projection) needed by renderers and shaders.
/// </para>
/// <para>
/// The combined view-projection matrix is commonly used in shaders to transform vertices from world
/// space to clip space in a single operation.
/// </para>
/// </remarks>
public interface ICamera
{
    /// <summary>
    /// Gets or sets the view matrix that transforms from world space to view (camera) space.
    /// </summary>
    /// <remarks>
    /// The view matrix represents the camera's position and orientation in the world.
    /// It transforms coordinates from world space to the camera's local coordinate system,
    /// where the camera is at the origin looking down the negative Z-axis.
    /// </remarks>
    Matrix4x4 ViewMatrix { get; set; }

    /// <summary>
    /// Gets or sets the projection matrix that transforms from view space to clip space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The projection matrix defines how the 3D scene is projected onto the 2D screen.
    /// It can represent either a perspective projection (with foreshortening for depth)
    /// or an orthographic projection (parallel projection without perspective).
    /// </para>
    /// <para>
    /// For perspective projection, this is typically created using field of view, aspect ratio,
    /// and near/far clipping planes. For orthographic projection, it uses left, right, top,
    /// bottom, near, and far bounds.
    /// </para>
    /// </remarks>
    Matrix4x4 ProjectionMatrix { get; set; }

    /// <summary>
    /// Gets or sets the combined view-projection matrix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This matrix is the product of the view matrix and projection matrix (Projection × View).
    /// It transforms coordinates directly from world space to clip space, combining both
    /// transformations in a single matrix multiplication.
    /// </para>
    /// <para>
    /// Many renderers use this combined matrix for efficiency, avoiding the need to multiply
    /// view and projection matrices separately in shaders for each vertex.
    /// </para>
    /// </remarks>
    Matrix4x4 ViewProjection { get; set; }

    /// <summary>
    /// Updates the camera's matrices based on its current state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should recalculate the view matrix, projection matrix, and view-projection
    /// matrix based on the camera's current position, orientation, field of view, aspect ratio,
    /// and other relevant parameters.
    /// </para>
    /// <para>
    /// Implementations typically call this method each frame or whenever camera properties
    /// change (e.g., when the camera moves, rotates, or the window is resized changing the
    /// aspect ratio).
    /// </para>
    /// </remarks>
    void Update();
}