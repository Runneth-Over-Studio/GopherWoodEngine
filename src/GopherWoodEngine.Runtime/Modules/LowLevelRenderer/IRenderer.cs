using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines the contract for the main renderer responsible for orchestrating the rendering pipeline.
/// </summary>
/// <remarks>
/// <para>
/// The renderer manages the execution of registered sub-renderers during the render pass,
/// coordinating frame acquisition, render pass execution, and command submission to the GPU.
/// </para>
/// <para>
/// Sub-renderers can be dynamically registered and unregistered, allowing for flexible composition
/// of rendering features such as geometry rendering, skybox, UI overlays, and debug visualizations.
/// Each sub-renderer executes in priority order based on its <see cref="ISubRenderer.RenderOrder"/> value.
/// </para>
/// </remarks>
public interface IRenderer : IDisposable
{
    /// <summary>
    /// Registers a sub-renderer to be executed during rendering.
    /// </summary>
    /// <param name="renderer">The sub-renderer to register.</param>
    void RegisterSubRenderer(ISubRenderer renderer);

    /// <summary>
    /// Unregisters a previously registered sub-renderer.
    /// </summary>
    /// <param name="renderer">The sub-renderer to unregister.</param>
    /// <returns><c>true</c> if the sub-renderer was found and removed; otherwise, <c>false</c>.</returns>
    bool UnregisterSubRenderer(ISubRenderer renderer);

    /// <summary>
    /// Removes all registered sub-renderers.
    /// </summary>
    void ClearSubRenderers();

    /// <summary>
    /// Executes the main render loop for a single frame.
    /// </summary>
    void Render(ICamera camera);
}
