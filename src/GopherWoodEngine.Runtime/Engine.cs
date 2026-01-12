using GopherWoodEngine.Runtime.Modules;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Silk.NET.Windowing;
using System;

namespace GopherWoodEngine.Runtime;

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

    /// <summary>
    /// Gets the wave audio player for playing 2D and 3D positional sound effects and music.
    /// </summary>
    /// <remarks>
    /// The wave player supports PCM-encoded WAVE files (mono or stereo, 8 or 16 bits per sample).
    /// All playback is fire-and-forget and non-blocking.
    /// </remarks>
    public IWavePlayer WavePlayer { get; }

    private readonly IWindow _window;
    private readonly IRenderer _renderer;
    private readonly IPhysicalDeviceIO _physicalDeviceIO;
    private readonly ILogger<Engine> _logger;
    private readonly GameBase _game;
    private readonly ICamera _camera; //TODO: Remove when scene management is added.
    private bool _isRunning = true;
    private bool _isSuspended = false;
    private bool _isDisposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="Engine"/> class, setting up the core services and event
    /// subscriptions required for the game engine.
    /// </summary>
    public Engine(GameBase game)
    {
        IServiceCollection services = new ServiceCollection().AddEngineServices(game.EngineConfig);
        IServiceProvider provider = services.BuildServiceProvider();
        Ioc.Default.ConfigureServices(provider);

        _window = Ioc.Default.GetRequiredService<IWindow>();
        _renderer = Ioc.Default.GetRequiredService<IRenderer>();
        _physicalDeviceIO = Ioc.Default.GetRequiredService<IPhysicalDeviceIO>();
        _logger = Ioc.Default.GetRequiredService<ILogger<Engine>>();
        _game = game;
        _camera = new DefaultCamera(_window, 70.0f * MathF.PI / 180.0f, 0.01f, 1000.0f);

        EventSystem = Ioc.Default.GetRequiredService<IEventSystem>();
        WavePlayer = Ioc.Default.GetRequiredService<IWavePlayer>();

        EventSystem.Subscribe<WindowUpdateEventArgs>(OnUpdate);
        EventSystem.Subscribe<WindowRenderEventArgs>(OnRender);
        EventSystem.Subscribe<WindowResizeEventArgs>(OnResize);
        EventSystem.Subscribe<WindowCloseEventArgs>(OnWindowClosing);

        // Temp manual test.
        //_renderer.RegisterSubRenderer(new ModelRenderer());

        _logger.LogDebug("Engine initialized.");
    }

    /// <summary>
    /// Starts the game loop.
    /// </summary>
    public void Run()
    {
        _game.Initialize();

        _logger.LogDebug("Initiating game loop...");

        _window.Run();

        _logger.LogDebug("Exited game loop.");
    }

    private void OnUpdate(object? sender, WindowUpdateEventArgs e)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Update(e.DeltaTime);
            _camera.Update();
            WavePlayer.Update();
        }
    }

    private void OnRender(object? sender, WindowRenderEventArgs e)
    {
        if (_isRunning && !_isSuspended)
        {
            _game.Render(e.DeltaTime);
            _renderer.Render(_camera);
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
        if (!_isDisposed)
        {
            if (disposing)
            {
                _isRunning = false;

                WavePlayer.Dispose();
                _renderer.Dispose();
                _window.Dispose();
                EventSystem.Dispose();

                _logger.LogDebug("Engine disposed.");

                Serilog.Log.CloseAndFlush();
            }

            _isDisposed = true;
        }
    }
}
