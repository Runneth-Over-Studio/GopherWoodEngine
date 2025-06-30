using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// The window load event.
/// </summary>
public class WindowLoadEventArgs : EventArgs
{
    // This class can be extended in the future if needed.
}

/// <summary>
/// The window update event.
/// </summary>
public class WindowUpdateEventArgs(double deltaTime = 0D) : EventArgs
{
    /// <summary>
    /// Frame delta time, in seconds.
    /// </summary>
    public double DeltaTime { get; } = deltaTime;
}

/// <summary>
/// The window rendering event.
/// </summary>
public class WindowRenderEventArgs(double deltaTime = 0D) : EventArgs
{
    /// <summary>
    /// Frame delta time, in seconds.
    /// </summary>
    public double DeltaTime { get; } = deltaTime;
}

/// <summary>
/// The window resize event.
/// </summary>
public class WindowResizeEventArgs(int width, int height) : EventArgs
{
    /// <summary>
    /// Gets the width of the window in pixels.
    /// </summary>
    public int Width { get; } = width;

    /// <summary>
    /// Gets the height of the window in pixels.
    /// </summary>
    public int Height { get; } = height;
}

/// <summary>
/// The framebuffer resize event of a window.
/// </summary>
public class WindowFramebufferResizeEventArgs(int width, int height) : EventArgs
{
    /// <summary>
    /// Gets the width of the window in pixels.
    /// </summary>
    public int Width { get; } = width;

    /// <summary>
    /// Gets the height of the window in pixels.
    /// </summary>
    public int Height { get; } = height;
}

/// <summary>
/// The event that is raised when the focus state of a window changes.
/// </summary>
public class WindowFocusChangedEventArgs(bool focused) : EventArgs
{
    /// <summary>
    /// Indicates whether the game window has input focus.
    /// </summary>
    public bool Focused { get; } = focused;
}

/// <summary>
/// Event that is raised when a window is about to close.
/// </summary>
public class WindowCloseEventArgs : EventArgs
{
    // This class can be extended in the future if needed.
}
