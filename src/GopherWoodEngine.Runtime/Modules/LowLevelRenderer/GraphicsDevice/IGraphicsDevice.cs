using System;
using Silk.NET.Input;

namespace GopherWoodEngine.Runtime.Modules;

public interface IGraphicsDevice : IDisposable
{
    void HookWindowEvents(IEventSystem eventSystem);

    IInputContext GetWindowInputContext();

    void InitiateWindowMessageLoop();

    void Shutdown();
}
