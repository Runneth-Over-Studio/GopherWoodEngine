namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a texture image resource.
/// </summary>
/// <remarks>
/// This is an API-agnostic representation. The actual GPU resource
/// is managed by the renderer backend.
/// </remarks>
public sealed class Texture
{
    public required string FilePath { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    // Opaque handle to backend-specific texture resource
    internal object? BackendHandle { get; set; }
}
