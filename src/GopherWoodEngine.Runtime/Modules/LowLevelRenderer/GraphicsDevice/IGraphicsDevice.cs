using System;
using Silk.NET.Input;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Represents a graphics device that provides windowing abstractions and functionality for interfacing with the GPU.
/// </summary>
/// <remarks>
/// Implements <see cref="IDisposable"/> to ensure proper cleanup of resources.
/// </remarks>
public interface IGraphicsDevice : IDisposable
{
    /// <summary>
    /// Hooks window-related events into the specified event system.
    /// </summary>
    void HookWindowEvents(IEventSystem eventSystem);

    /// <summary>
    /// Creates and returns a new input context for a window.
    /// </summary>
    /// <remarks>
    /// The returned input context can be used to manage and process input events for the game
    /// window.
    /// </remarks>
    IInputContext CreateWindowInputContext();

    /// <summary>
    /// Starts the message loop for the application's main window, enabling it to process user input and system events.
    /// </summary>
    /// <remarks>
    /// Ensure that the application's main window is properly initialized before invoking this method.
    /// </remarks>
    void InitiateWindowMessageLoop();

    /// <summary>
    /// Shuts down the game window.
    /// </summary>
    void Shutdown();
}
