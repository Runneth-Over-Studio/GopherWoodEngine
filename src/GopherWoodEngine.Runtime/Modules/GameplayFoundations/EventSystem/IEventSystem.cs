using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Defines a system for publishing and subscribing to events.
/// </summary>
public interface IEventSystem : IDisposable
{
    /// <summary>
    /// Publishes an event to all registered subscribers.
    /// </summary>
    void Publish<T>(object? sender, T eventData) where T : EventArgs;

    /// <summary>
    /// Subscribes to an event with the specified event handler.
    /// </summary>
    void Subscribe<T>(EventHandler<T> handler) where T : EventArgs;

    /// <summary>
    /// Removes a previously subscribed event handler from the event source.
    /// </summary>
    void Unsubscribe<T>(EventHandler<T> handler) where T : EventArgs;
}
