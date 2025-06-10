using System;

namespace GopherWoodEngine.Runtime.Modules;

public interface IEventSystem : IDisposable
{
    void Publish<T>(object? sender, T eventData) where T : EventArgs;

    void Subscribe<T>(EventHandler<T> handler) where T : EventArgs;

    void Unsubscribe<T>(EventHandler<T> handler) where T : EventArgs;
}
