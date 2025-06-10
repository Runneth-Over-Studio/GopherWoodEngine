using GopherWoodEngine.Runtime.Core;
using System;

namespace GopherWoodEngine.Runtime;

public abstract class Game : IDisposable
{
    public EngineConfig EngineConfig { get; }
    public Engine Engine { get; private set; }

    private bool _disposed = false;

    public Game(EngineConfig engineConfig)
    {
        EngineConfig = engineConfig;
        Engine = new Engine(this);
    }

    public virtual void Start()
    {
        Engine.Run();
    }

    public virtual void Update(double deltaTime) { }

    public virtual void Render(double deltaTime) { }

    public virtual void OnResize(int width, int height) { }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                Engine.Dispose();
            }

            _disposed = true;
        }
    }
}
