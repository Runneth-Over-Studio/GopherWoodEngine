using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace RunnethOverStudio.GopherWoodEngine.Runtime.Modules;

internal class EventSystem : IEventSystem
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
    private readonly ConcurrentDictionary<Type, object> _locks = new();

    public void Publish<T>(object? sender, T eventData) where T : EventArgs
    {
        Type key = typeof(EventHandler<T>);

        if (_handlers.TryGetValue(key, out List<Delegate>? handlers))
        {
            lock (_locks.GetOrAdd(key, _ => new object()))
            {
                foreach (EventHandler<T> handler in handlers.Cast<EventHandler<T>>())
                {
                    handler(sender, eventData);
                }
            }
        }
    }

    public void Subscribe<T>(EventHandler<T> handler) where T : EventArgs
    {
        Type key = typeof(EventHandler<T>);
        List<Delegate> list = _handlers.GetOrAdd(key, _ => []);

        lock (_locks.GetOrAdd(key, _ => new object()))
        {
            list.Add(handler);
        }
    }

    public void Unsubscribe<T>(EventHandler<T> handler) where T : EventArgs
    {
        Type key = typeof(EventHandler<T>);

        if (_handlers.TryGetValue(key, out List<Delegate>? list))
        {
            lock (_locks.GetOrAdd(key, _ => new object()))
            {
                list.Remove(handler);
            }
        }
    }
}
