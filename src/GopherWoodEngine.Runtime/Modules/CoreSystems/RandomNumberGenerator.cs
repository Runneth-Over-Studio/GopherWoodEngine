using System;

namespace GopherWoodEngine.Runtime.Modules;

internal sealed class RandomNumberGenerator : IRandomNumberGenerator
{
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="RandomNumberGenerator"/> class.
    /// </summary>
    /// <param name="seed">
    /// An optional seed value to initialize the random number generator.
    /// If <c>null</c>, creates a thread-safe shared instance.
    /// If provided, creates an instance with the specified seed for reproducible results, but for single-threaded contexts only.
    /// </param>
    /// <remarks>
    /// Use seeded instances in single-threaded contexts (like deterministic game logic on the main thread).
    /// Use seed: <c>null</c> when you need thread-safety for concurrent operations.
    /// </remarks>
    public RandomNumberGenerator(int? seed = null)
    {
        _random = seed != null ? new Random(seed.Value) : Random.Shared;
    }

    public string GetHexString(int stringLength, bool lowercase = false) => _random.GetHexString(stringLength, lowercase);

    public void GetHexString(Span<char> destination, bool lowercase = false) => _random.GetHexString(destination, lowercase);

    public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination) => _random.GetItems<T>(choices, destination);

    public T[] GetItems<T>(T[] choices, int length) => _random.GetItems<T>(choices, length);

    public T[] GetItems<T>(ReadOnlySpan<T> choices, int length) => _random.GetItems<T>(choices, length);

    public string GetString(ReadOnlySpan<char> choices, int length) => _random.GetString(choices, length);

    public int Next() => _random.Next();

    public int Next(int maxValue) => _random.Next(maxValue);

    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

    public void NextBytes(byte[] buffer) => _random.NextBytes(buffer);

    public void NextBytes(Span<byte> buffer) => _random.NextBytes(buffer);

    public double NextDouble() => _random.NextDouble();

    public long NextInt64() => _random.NextInt64();

    public long NextInt64(long maxValue) => _random.NextInt64(maxValue);

    public long NextInt64(long minValue, long maxValue) => _random.NextInt64(minValue, maxValue);

    public float NextSingle() => _random.NextSingle();

    public void Shuffle<T>(T[] values) => _random.Shuffle<T>(values);

    public void Shuffle<T>(Span<T> values) => _random.Shuffle<T>(values);
}
