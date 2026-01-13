namespace GopherWoodEngine.Runtime.Modules.Rendering;

/// <summary>
/// Represents a shader program.
/// </summary>
public sealed class Shader
{
    public required string Name { get; init; }
    public required string VertexShaderPath { get; init; }
    public required string FragmentShaderPath { get; init; }

    // Opaque handle to backend-specific shader resource
    internal object? BackendHandle { get; set; }
}
