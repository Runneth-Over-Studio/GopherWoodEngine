using System;

namespace GopherWoodEngine.Runtime.Modules;

internal sealed class RandomNumberGenerator : IRandomNumberGenerator
{
    private readonly Random _random = Random.Shared;

    public string GetHexString(int stringLength, bool lowercase = false)
    {
        return _random.GetHexString(stringLength, lowercase);
    }

    public void GetHexString(Span<char> destination, bool lowercase = false)
    {
        _random.GetHexString(destination, lowercase);
    }

    public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
    {
        _random.GetItems<T>(choices, destination);
    }

    public T[] GetItems<T>(T[] choices, int length)
    {
        return _random.GetItems<T>(choices, length);
    }

    public T[] GetItems<T>(ReadOnlySpan<T> choices, int length)
    {
        return _random.GetItems<T>(choices, length);
    }

    public string GetString(ReadOnlySpan<char> choices, int length)
    {
        return _random.GetString(choices, length);
    }

    public int Next()
    {
        return _random.Next();
    }

    public int Next(int maxValue)
    {
        return _random.Next(maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        return _random.Next(minValue, maxValue);
    }

    public void NextBytes(byte[] buffer)
    {
        _random.NextBytes(buffer);
    }

    public void NextBytes(Span<byte> buffer)
    {
        _random.NextBytes(buffer);
    }

    public double NextDouble()
    {
        return _random.NextDouble();
    }

    public long NextInt64()
    {
        return _random.NextInt64();
    }

    public long NextInt64(long maxValue)
    {
        return _random.NextInt64(maxValue);
    }

    public long NextInt64(long minValue, long maxValue)
    {
        return _random.NextInt64(minValue, maxValue);
    }

    public float NextSingle()
    {
        return _random.NextSingle();
    }

    public void Shuffle<T>(T[] values)
    {
        _random.Shuffle<T>(values);
    }

    public void Shuffle<T>(Span<T> values)
    {
        _random.Shuffle<T>(values);
    }
}
