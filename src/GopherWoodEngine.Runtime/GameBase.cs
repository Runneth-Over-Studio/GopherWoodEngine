using GopherWoodEngine.Runtime.Core;
using System;

namespace GopherWoodEngine.Runtime;

/// <summary>
/// Represents the base class for a game application, providing core functionality such as engine configuration, game
/// loop management, and lifecycle methods.
/// </summary>
/// <remarks>
/// The <see cref="GameBase"/> class serves as the foundation for creating game applications using the Gopher
/// Wood Engine. Derived classes should override the virtual methods to implement specific game logic. 
/// This class also implements <see cref="IDisposable"/> to ensure proper cleanup of resources.
/// </remarks>
public abstract class GameBase : IDisposable
{
    /// <summary>
    /// The configuration settings for the engine.
    /// </summary>
    public EngineConfig EngineConfig { get; }

    /// <summary>
    /// Gopher Wood Engine.
    /// </summary>
    public Engine Engine { get; private set; }

    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameBase"/> class with the specified engine configuration.
    /// </summary>
    public GameBase(EngineConfig engineConfig)
    {
        EngineConfig = engineConfig;
        Engine = new Engine(this);
    }

    /// <summary>
    /// Starts the engine, initiating the main game loop.
    /// </summary>
    /// <remarks>
    /// Derived classes can override this method to implement specific startup logic, 
    /// but base.Start() or its own call to Engine.Run() should always be called last.
    /// </remarks>
    public virtual void Start()
    {
        Engine.Run();
    }

    /// <summary>
    /// Update game states, optionally based on delta time, prior to next frame render.
    /// </summary>
    /// <remarks>
    /// Derived classes can override this method to implement specific update logic.
    /// </remarks>
    public virtual void Update(double deltaTime) { }

    /// <summary>
    /// Render the current frame.
    /// </summary>
    /// <remarks>
    /// Implementations may use <paramref name="deltaTime"/> to calculate animations, 
    /// transitions, or other time-dependent effects.
    /// </remarks>
    public virtual void Render(double deltaTime) { }

    /// <summary>
    /// Implement custom resizing behavior upon game window dimension changes.
    /// </summary>
    public virtual void OnResize(int width, int height) { }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the resources used by the current instance of the class if <paramref name="disposing"/> is <see langword="true"/>.
    /// </summary>
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
