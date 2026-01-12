using Silk.NET.Windowing;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Provides extension methods for the <see cref="IWindow"/> interface.
/// </summary>
public static class IWindowExtensions
{
    /// <summary>
    /// Calculates the aspect ratio of the window based on its current size.
    /// </summary>
    /// <param name="window">The window for which to calculate the aspect ratio.</param>
    /// <returns>
    /// The aspect ratio as a floating-point value, calculated as width divided by height.
    /// </returns>
    /// <remarks>
    /// The aspect ratio is commonly used for setting up projection matrices in 3D rendering
    /// to ensure proper perspective without distortion. A value greater than 1 indicates a
    /// landscape orientation, while a value less than 1 indicates portrait orientation.
    /// </remarks>
    public static float AspectRatio(this IWindow window)
    {
        return (float)window.Size.X / (float)window.Size.Y;
    }
}
