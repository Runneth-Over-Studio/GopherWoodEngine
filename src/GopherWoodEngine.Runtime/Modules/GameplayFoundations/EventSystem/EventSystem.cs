using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace GopherWoodEngine.Runtime.Modules;

internal class EventSystem(ILogger<IEventSystem> logger) : IEventSystem
{
    private readonly ConcurrentDictionary<Type, ImmutableList<Delegate>> _handlers = new();
    private readonly ILogger<IEventSystem> _logger = logger;
    private bool _disposed = false;

    public void Publish<T>(object? sender, T eventData) where T : EventArgs
    {
        if (!_disposed)
        {
            Type key = typeof(EventHandler<T>);

            if (_handlers.TryGetValue(key, out ImmutableList<Delegate>? handlers))
            {
                // The use of the immutable collection allows for lock-free reads.
                foreach (EventHandler<T> handler in handlers.Cast<EventHandler<T>>())
                {
                    try
                    {
                        handler(sender, eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while invoking event handler for {EventType}.", key.Name);
                    }
                }
            }
        }
        else
        {
            throw new ObjectDisposedException(nameof(EventSystem));
        }
    }

    public void Subscribe<T>(EventHandler<T> handler) where T : EventArgs
    {
        if (!_disposed)
        {
            Type key = typeof(EventHandler<T>);
            _handlers.AddOrUpdate(key, [handler], (k, list) => list.Contains(handler) ? list : list.Add(handler));
        }
        else
        {
            throw new ObjectDisposedException(nameof(EventSystem));
        }
    }

    public void Unsubscribe<T>(EventHandler<T> handler) where T : EventArgs
    {
        if (!_disposed)
        {
            Type key = typeof(EventHandler<T>);
            _handlers.AddOrUpdate(key, [], (k, list) => list.Remove(handler));
        }
        else
        {
            throw new ObjectDisposedException(nameof(EventSystem));
        }
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
                _handlers.Clear();
            }

            _disposed = true;
        }
    }
}
