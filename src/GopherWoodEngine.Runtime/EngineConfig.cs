using System;

namespace GopherWoodEngine.Runtime;

public record EngineConfig
{
    public required string Title { get; set; }
    public int Width { get; set; } = 1280;
    public int Height { get; set; } = 720;
}
