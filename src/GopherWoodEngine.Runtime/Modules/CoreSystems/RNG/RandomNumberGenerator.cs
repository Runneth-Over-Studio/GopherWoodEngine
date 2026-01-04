using System;
using System.Threading;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Provides a thread-safe implementation of <see cref="IRandomNumberGenerator"/> that wraps <see cref="Random"/>.
/// </summary>
/// <remarks>
/// All methods are protected by a lock to ensure thread-safety when the instance is shared across multiple threads.
/// Each instance maintains its own independent random number sequence.
/// </remarks>
public sealed class RandomNumberGenerator : IRandomNumberGenerator
{
    private readonly Random _random;
    private readonly Lock _lock;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomNumberGenerator"/> class.
    /// </summary>
    /// <param name="seed">
    /// An optional seed value to initialize the random number generator.
    /// If <c>null</c>, the generator is initialized with a time-dependent default seed.
    /// If provided, creates a deterministic sequence for reproducible results.
    /// </param>
    /// <remarks>
    /// This implementation is thread-safe and can be safely shared across multiple threads.
    /// Use a specific seed for deterministic behavior (e.g., in replays, networked games, or unit tests).
    /// Use <c>null</c> for non-deterministic randomness.
    /// </remarks>
    public RandomNumberGenerator(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _lock = new Lock();
    }

    /// <inheritdoc/>
    public string GetHexString(int stringLength, bool lowercase = false)
    {
        lock (_lock)
        {
            return _random.GetHexString(stringLength, lowercase);
        }
    }

    /// <inheritdoc/>
    public void GetHexString(Span<char> destination, bool lowercase = false)
    {
        lock (_lock)
        {
            _random.GetHexString(destination, lowercase);
        }
    }

    /// <inheritdoc/>
    public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
    {
        lock (_lock)
        {
            _random.GetItems<T>(choices, destination);
        }
    }

    /// <inheritdoc/>
    public T[] GetItems<T>(T[] choices, int length)
    {
        lock (_lock)
        {
            return _random.GetItems<T>(choices, length);
        }
    }

    /// <inheritdoc/>
    public T[] GetItems<T>(ReadOnlySpan<T> choices, int length)
    {
        lock (_lock)
        {
            return _random.GetItems<T>(choices, length);
        }
    }

    /// <inheritdoc/>
    public string GetString(ReadOnlySpan<char> choices, int length)
    {
        lock (_lock)
        {
            return _random.GetString(choices, length);
        }
    }

    /// <inheritdoc/>
    public int Next()
    {
        lock (_lock)
        {
            return _random.Next();
        }
    }

    /// <inheritdoc/>
    public int Next(int maxValue)
    {
        lock (_lock)
        {
            return _random.Next(maxValue);
        }
    }

    /// <inheritdoc/>
    public int Next(int minValue, int maxValue)
    {
        lock (_lock)
        {
            return _random.Next(minValue, maxValue);
        }
    }

    /// <inheritdoc/>
    public void NextBytes(byte[] buffer)
    {
        lock (_lock)
        {
            _random.NextBytes(buffer);
        }
    }

    /// <inheritdoc/>
    public void NextBytes(Span<byte> buffer)
    {
        lock (_lock)
        {
            _random.NextBytes(buffer);
        }
    }

    /// <inheritdoc/>
    public double NextDouble()
    {
        lock (_lock)
        {
            return _random.NextDouble();
        }
    }

    /// <inheritdoc/>
    public long NextInt64()
    {
        lock (_lock)
        {
            return _random.NextInt64();
        }
    }

    /// <inheritdoc/>
    public long NextInt64(long maxValue)
    {
        lock (_lock)
        {
            return _random.NextInt64(maxValue);
        }
    }

    /// <inheritdoc/>
    public long NextInt64(long minValue, long maxValue)
    {
        lock (_lock)
        {
            return _random.NextInt64(minValue, maxValue);
        }
    }

    /// <inheritdoc/>
    public float NextSingle()
    {
        lock (_lock)
        {
            return _random.NextSingle();
        }
    }

    /// <inheritdoc/>
    public void Shuffle<T>(T[] values)
    {
        lock (_lock)
        {
            _random.Shuffle<T>(values);
        }
    }

    /// <inheritdoc/>
    public void Shuffle<T>(Span<T> values)
    {
        lock (_lock)
        {
            _random.Shuffle<T>(values);
        }
    }
}
