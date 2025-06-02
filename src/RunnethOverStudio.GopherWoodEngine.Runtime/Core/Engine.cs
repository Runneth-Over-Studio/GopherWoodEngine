using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RunnethOverStudio.GopherWoodEngine.Runtime.Modules;
using System;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Core;

public class Engine : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IGraphicsDevice _graphicsDevice;
    private readonly ILogger<Engine> _logger;
    private readonly Game _game;
    private bool _isRunning = true;
    private bool _isSuspended = false;
    private bool _disposed = false;

    public Engine(Game game)
    {
        _services = EngineBuilder.Build(game.EngineConfig);
        _graphicsDevice = _services.GetRequiredService<IGraphicsDevice>();
        _logger = _services.GetRequiredService<ILogger<Engine>>();
        _game = game;

        _graphicsDevice.Update += OnUpdate;
        _graphicsDevice.Render += OnRender;
        _graphicsDevice.Resize += OnResize;
        _graphicsDevice.Closing += OnWindowClosing;

        _logger.LogDebug("Engine initialized.");
    }

    public void Run()
    {
        _logger.LogDebug("Initiating engine main game loop.");
        _graphicsDevice.InitiateWindowMessageLoop();
    }

    private void OnUpdate(double deltaTime)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Update(deltaTime);
        }
    }

    private void OnRender(double deltaTime)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Render(deltaTime);
        }
    }

    private void OnResize(int width, int height)
    {
        _game.OnResize(width, height);
    }

    private void OnWindowClosing()
    {
        _logger.LogDebug("Window closing, stopping engine.");
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
                _graphicsDevice.Closing -= OnWindowClosing;
                _graphicsDevice.Resize -= OnResize;
                _graphicsDevice.Render -= OnRender;
                _graphicsDevice.Update -= OnUpdate;

                _graphicsDevice.Dispose();

                _logger.LogDebug("Engine disposed.");
            }

            _disposed = true;
        }
    }
}
