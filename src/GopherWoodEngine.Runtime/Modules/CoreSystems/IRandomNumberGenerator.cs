using System;

namespace GopherWoodEngine.Runtime.Modules;

/// <summary>
/// Represents a pseudo-random number generator, which is an algorithm that produces a sequence of numbers
/// that meet certain statistical requirements for randomness.
/// </summary>
public interface IRandomNumberGenerator
{
    /// <summary>Returns a non-negative random integer.</summary>
    /// <returns>A 32-bit signed integer that is greater than or equal to 0 and less than <see cref="int.MaxValue"/>.</returns>
    int Next();

    /// <summary>Returns a non-negative random integer that is less than the specified maximum.</summary>
    /// <param name="maxValue">The exclusive upper bound of the random number to be generated. <paramref name="maxValue"/> must be greater than or equal to 0.</param>
    /// <returns>
    /// A 32-bit signed integer that is greater than or equal to 0, and less than <paramref name="maxValue"/>; that is, the range of return values ordinarily
    /// includes 0 but not <paramref name="maxValue"/>. However, if <paramref name="maxValue"/> equals 0, <paramref name="maxValue"/> is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is less than 0.</exception>
    int Next(int maxValue);

    /// <summary>Returns a random integer that is within a specified range.</summary>
    /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxValue">The exclusive upper bound of the random number returned. <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
    /// <returns>
    /// A 32-bit signed integer greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/>; that is, the range of return values includes <paramref name="minValue"/>
    /// but not <paramref name="maxValue"/>. If minValue equals <paramref name="maxValue"/>, <paramref name="minValue"/> is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    int Next(int minValue, int maxValue);

    /// <summary>Returns a non-negative random integer.</summary>
    /// <returns>A 64-bit signed integer that is greater than or equal to 0 and less than <see cref="long.MaxValue"/>.</returns>
    long NextInt64();

    /// <summary>Returns a non-negative random integer that is less than the specified maximum.</summary>
    /// <param name="maxValue">The exclusive upper bound of the random number to be generated. <paramref name="maxValue"/> must be greater than or equal to 0.</param>
    /// <returns>
    /// A 64-bit signed integer that is greater than or equal to 0, and less than <paramref name="maxValue"/>; that is, the range of return values ordinarily
    /// includes 0 but not <paramref name="maxValue"/>. However, if <paramref name="maxValue"/> equals 0, <paramref name="maxValue"/> is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxValue"/> is less than 0.</exception>
    long NextInt64(long maxValue);

    /// <summary>Returns a random integer that is within a specified range.</summary>
    /// <param name="minValue">The inclusive lower bound of the random number returned.</param>
    /// <param name="maxValue">The exclusive upper bound of the random number returned. <paramref name="maxValue"/> must be greater than or equal to <paramref name="minValue"/>.</param>
    /// <returns>
    /// A 64-bit signed integer greater than or equal to <paramref name="minValue"/> and less than <paramref name="maxValue"/>; that is, the range of return values includes <paramref name="minValue"/>
    /// but not <paramref name="maxValue"/>. If minValue equals <paramref name="maxValue"/>, <paramref name="minValue"/> is returned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minValue"/> is greater than <paramref name="maxValue"/>.</exception>
    long NextInt64(long minValue, long maxValue);

    /// <summary>Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.</summary>
    /// <returns>A single-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
    float NextSingle();

    /// <summary>Returns a random floating-point number that is greater than or equal to 0.0, and less than 1.0.</summary>
    /// <returns>A double-precision floating point number that is greater than or equal to 0.0, and less than 1.0.</returns>
    double NextDouble();

    /// <summary>Fills the elements of a specified array of bytes with random numbers.</summary>
    /// <param name="buffer">The array to be filled with random numbers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="buffer"/> is null.</exception>
    void NextBytes(byte[] buffer);

    /// <summary>Fills the elements of a specified span of bytes with random numbers.</summary>
    /// <param name="buffer">The array to be filled with random numbers.</param>
    void NextBytes(Span<byte> buffer);

    /// <summary>
    ///   Fills the elements of a specified span with items chosen at random from the provided set of choices.
    /// </summary>
    /// <param name="choices">The items to use to populate the span.</param>
    /// <param name="destination">The span to be filled with items.</param>
    /// <typeparam name="T">The type of span.</typeparam>
    /// <exception cref="ArgumentException"><paramref name="choices" /> is empty.</exception>
    /// <remarks>
    ///   The method uses <see cref="Next(int)" /> to select items randomly from <paramref name="choices" />
    ///   by index and populate <paramref name="destination" />.
    /// </remarks>
    void GetItems<T>(ReadOnlySpan<T> choices, Span<T> destination);

    /// <summary>
    ///   Creates an array populated with items chosen at random from the provided set of choices.
    /// </summary>
    /// <param name="choices">The items to use to populate the array.</param>
    /// <param name="length">The length of array to return.</param>
    /// <typeparam name="T">The type of array.</typeparam>
    /// <returns>An array populated with random items.</returns>
    /// <exception cref="ArgumentException"><paramref name="choices" /> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="choices" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="length" /> is not zero or a positive number.
    /// </exception>
    /// <remarks>
    ///   The method uses <see cref="Next(int)" /> to select items randomly from <paramref name="choices" />
    ///   by index. This is used to populate a newly-created array.
    /// </remarks>
    T[] GetItems<T>(T[] choices, int length);

    /// <summary>
    ///   Creates an array populated with items chosen at random from the provided set of choices.
    /// </summary>
    /// <param name="choices">The items to use to populate the array.</param>
    /// <param name="length">The length of array to return.</param>
    /// <typeparam name="T">The type of array.</typeparam>
    /// <returns>An array populated with random items.</returns>
    /// <exception cref="ArgumentException"><paramref name="choices" /> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <paramref name="length" /> is not zero or a positive number.
    /// </exception>
    /// <remarks>
    ///   The method uses <see cref="Next(int)" /> to select items randomly from <paramref name="choices" />
    ///   by index. This is used to populate a newly-created array.
    /// </remarks>
    T[] GetItems<T>(ReadOnlySpan<T> choices, int length);

    /// <summary>
    ///   Performs an in-place shuffle of an array.
    /// </summary>
    /// <param name="values">The array to shuffle.</param>
    /// <typeparam name="T">The type of array.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="values" /> is <see langword="null" />.</exception>
    /// <remarks>
    ///   This method uses <see cref="Next(int, int)" /> to choose values for shuffling.
    ///   This method is an O(n) operation.
    /// </remarks>
    void Shuffle<T>(T[] values);

    /// <summary>
    ///   Performs an in-place shuffle of a span.
    /// </summary>
    /// <param name="values">The span to shuffle.</param>
    /// <typeparam name="T">The type of span.</typeparam>
    /// <remarks>
    ///   This method uses <see cref="Next(int, int)" /> to choose values for shuffling.
    ///   This method is an O(n) operation.
    /// </remarks>
    void Shuffle<T>(Span<T> values);

    /// <summary>Creates a string populated with characters chosen at random from <paramref name="choices"/>.</summary>
    /// <param name="choices">The characters to use to populate the string.</param>
    /// <param name="length">The length of string to return.</param>
    /// <returns>A string populated with items selected at random from <paramref name="choices"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="choices" /> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length" /> is not zero or a positive number.</exception>
    /// <seealso cref="GetItems{T}(ReadOnlySpan{T}, Span{T})" />
    string GetString(ReadOnlySpan<char> choices, int length);

    /// <summary>Creates a string filled with random hexadecimal characters.</summary>
    /// <param name="stringLength">The length of string to create.</param>
    /// <param name="lowercase">
    /// <see langword="true" /> if the hexadecimal characters should be lowercase; <see langword="false" /> if they should be uppercase.
    /// The default is <see langword="false" />.
    /// </param>
    /// <returns>A string populated with random hexadecimal characters.</returns>
    string GetHexString(int stringLength, bool lowercase = false);

    /// <summary>Fills a buffer with random hexadecimal characters.</summary>
    /// <param name="destination">The buffer to receive the characters.</param>
    /// <param name="lowercase">
    /// <see langword="true" /> if the hexadecimal characters should be lowercase; <see langword="false" /> if they should be uppercase.
    /// The default is <see langword="false" />.
    /// </param>
    void GetHexString(Span<char> destination, bool lowercase = false);
}
