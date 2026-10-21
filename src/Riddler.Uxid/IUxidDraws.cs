namespace Riddler;

/// <summary>
/// Where a <see cref="UxidMonotonic"/> generator takes its random values
/// from, in place of its byte source: a fresh random field, the seed, and the
/// step that moves the field on within a millisecond.
/// </summary>
/// <remarks>
/// An implementation of your own exists to replay known values, as a test or
/// a conformance case does, and is never a source of randomness for real ids:
/// a monotonic id is exactly as guessable as its draws. Without one, the
/// generator draws both values from its byte source, as
/// <see cref="UxidMonotonic"/> describes. The generator calls these methods
/// while it holds its lock, so they are never called concurrently by one
/// generator.
/// </remarks>
public interface IUxidDraws
{
    /// <summary>
    /// Returns a fresh random value for a random field of
    /// <paramref name="width"/> bytes: exactly that many bytes, read as one
    /// big-endian unsigned integer.
    /// </summary>
    /// <param name="width">The random field's width in bytes, from 1 to 10.</param>
    /// <returns>The field's bytes, most significant first.</returns>
    byte[] DrawSeed(int width);

    /// <summary>
    /// Returns a step for a random field of <paramref name="width"/> bytes,
    /// drawn uniformly from 1 to 2^(4 x width) inclusive, half the field's
    /// bit width.
    /// </summary>
    /// <param name="width">The random field's width in bytes, from 1 to 10.</param>
    /// <returns>The step, from 1 to 2^(4 x width).</returns>
    long DrawStep(int width);
}
