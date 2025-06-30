using GopherWoodEngine.Runtime.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace GopherWoodEngine.Runtime.Core;

/// <summary>
/// Represents the core engine responsible for managing the game loop and systems.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/> to ensure proper cleanup of resources.
/// </remarks>
public class Engine : IDisposable
{
    /// <summary>
    /// Gets the event system used to manage and dispatch application events.
    /// </summary>
    public IEventSystem EventSystem { get; }

    private readonly IServiceProvider _services;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IPhysicalDeviceIO _physicalDeviceIO;
    private readonly ILogger<Engine> _logger;
    private readonly Game _game;
    private bool _isRunning = true;
    private bool _isSuspended = false;
    private bool _disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="Engine"/> class, setting up the core services and event
    /// subscriptions required for the game engine.
    /// </summary>
    public Engine(Game game)
    {
        _services = EngineBuilder.Build(game.EngineConfig);
        EventSystem = _services.GetRequiredService<IEventSystem>();
        _graphicsDevice = _services.GetRequiredService<IGraphicsDevice>();
        _physicalDeviceIO = _services.GetRequiredService<IPhysicalDeviceIO>();
        _logger = _services.GetRequiredService<ILogger<Engine>>();
        _game = game;

        _graphicsDevice.HookWindowEvents(EventSystem);
        EventSystem.Subscribe<WindowUpdateEventArgs>(OnUpdate);
        EventSystem.Subscribe<WindowRenderEventArgs>(OnRender);
        EventSystem.Subscribe<WindowResizeEventArgs>(OnResize);
        EventSystem.Subscribe<WindowCloseEventArgs>(OnWindowClosing);

        _logger.LogDebug("Engine initialized.");
    }

    /// <summary>
    /// Starts the game loop.
    /// </summary>
    public void Run()
    {
        _logger.LogDebug("Initiating game loop...");

        _graphicsDevice.InitiateWindowMessageLoop();

        _logger.LogDebug("Exited game loop.");
    }

    private void OnUpdate(object? sender, WindowUpdateEventArgs e)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Update(e.DeltaTime);
        }
    }

    private void OnRender(object? sender, WindowRenderEventArgs e)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Render(e.DeltaTime);
        }
    }

    private void OnResize(object? sender, WindowResizeEventArgs e)
    {
        _game.OnResize(e.Width, e.Height);
    }

    private void OnWindowClosing(object? sender, WindowCloseEventArgs e)
    {
        _isRunning = false;
    }

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
                EventSystem.Dispose();
                _graphicsDevice.Dispose();

                _logger.LogDebug("Engine disposed.");
            }

            _disposed = true;
        }
    }
}
