using GopherWoodEngine.Runtime.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace GopherWoodEngine.Runtime.Core;

public class Engine : IDisposable
{
    public IEventSystem EventSystem { get; }

    private readonly IServiceProvider _services;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly IPhysicalDeviceIO _physicalDeviceIO;
    private readonly ILogger<Engine> _logger;
    private readonly Game _game;
    private bool _isRunning = true;
    private bool _isSuspended = false;
    private bool _disposed = false;

    public Engine(Game game)
    {
        _services = EngineBuilder.Build(game.EngineConfig);
        EventSystem = _services.GetRequiredService<IEventSystem>();
        _graphicsDevice = _services.GetRequiredService<IGraphicsDevice>();
        _physicalDeviceIO = _services.GetRequiredService<IPhysicalDeviceIO>();
        _logger = _services.GetRequiredService<ILogger<Engine>>();
        _game = game;

        IEventSystem eventSystem = _services.GetRequiredService<IEventSystem>();
        eventSystem.Subscribe<WindowUpdateEventArgs>(OnUpdate);
        eventSystem.Subscribe<WindowRenderEventArgs>(OnRender);
        eventSystem.Subscribe<WindowResizeEventArgs>(OnResize);
        eventSystem.Subscribe<WindowCloseEventArgs>(OnWindowClosing);

        _logger.LogDebug("Engine initialized.");
    }

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
                EventSystem.Dispose();
                _graphicsDevice.Dispose();

                _logger.LogDebug("Engine disposed.");
            }

            _disposed = true;
        }
    }
}
