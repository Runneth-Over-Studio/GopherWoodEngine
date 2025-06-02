using System;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Modules;

public interface IGraphicsDevice : IDisposable
{
    event Action? Load;
    event Action<double>? Update;
    event Action<double>? Render;
    event Action<int, int>? Resize;
    event Action<int, int>? FramebufferResize;
    event Action<bool>? FocusChanged;
    event Action? Closing;

    void InitiateWindowMessageLoop();

    void Shutdown();
}
