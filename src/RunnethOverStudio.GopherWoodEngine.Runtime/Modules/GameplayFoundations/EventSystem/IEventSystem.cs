using System;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Modules;

public interface IEventSystem
{
    void Publish<T>(object? sender, T eventData) where T : EventArgs;

    void Subscribe<T>(EventHandler<T> handler) where T : EventArgs;

    void Unsubscribe<T>(EventHandler<T> handler) where T : EventArgs;
}
