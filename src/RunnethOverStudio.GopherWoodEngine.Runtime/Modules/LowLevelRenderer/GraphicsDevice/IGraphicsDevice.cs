using System;
using Silk.NET.Input;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Modules;

public interface IGraphicsDevice : IDisposable
{
    IInputContext GetWindowInputContext();

    void InitiateWindowMessageLoop();

    void Shutdown();
}
