using System;

namespace GopherWoodEngine.Runtime.Modules;

internal sealed class RandomNumberGenerator : IRandomNumberGenerator
{
    public string GetHexString(int stringLength, bool lowercase = false)
    {
        return Random.Shared.GetHexString(stringLength, lowercase);
    }

    public void GetHexString(Span<char> destination, bool lowercase = false)
    {
        Random.Shared.GetHexString(destination, lowercase);
    }

    public void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination)
    {
        Random.Shared.GetItems<T>(choices, destination);
    }

    public T[] GetItems<T>(T[] choices, int length)
    {
        return Random.Shared.GetItems<T>(choices, length);
    }

    public T[] GetItems<T>(ReadOnlySpan<T> choices, int length)
    {
        return Random.Shared.GetItems<T>(choices, length);
    }

    public string GetString(ReadOnlySpan<char> choices, int length)
    {
        return Random.Shared.GetString(choices, length);
    }

    public int Next()
    {
        return Random.Shared.Next();
    }

    public int Next(int maxValue)
    {
        return Random.Shared.Next(maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        return Random.Shared.Next(minValue, maxValue);
    }

    public void NextBytes(byte[] buffer)
    {
        Random.Shared.NextBytes(buffer);
    }

    public void NextBytes(Span<byte> buffer)
    {
        Random.Shared.NextBytes(buffer);
    }

    public double NextDouble()
    {
        return Random.Shared.NextDouble();
    }

    public long NextInt64()
    {
        return Random.Shared.NextInt64();
    }

    public long NextInt64(long maxValue)
    {
        return Random.Shared.NextInt64(maxValue);
    }

    public long NextInt64(long minValue, long maxValue)
    {
        return Random.Shared.NextInt64(minValue, maxValue);
    }

    public float NextSingle()
    {
        return Random.Shared.NextSingle();
    }

    public void Shuffle<T>(T[] values)
    {
        Random.Shared.Shuffle<T>(values);
    }

    public void Shuffle<T>(Span<T> values)
    {
        Random.Shared.Shuffle<T>(values);
    }
}
