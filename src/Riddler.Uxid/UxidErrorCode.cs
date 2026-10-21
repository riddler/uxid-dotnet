namespace Riddler;

/// <summary>
/// Why a generation request was refused: the closed set of generation errors
/// the wire format names. <see cref="UxidException.CodeName"/> spells each
/// one as the wire format and its conformance corpus do. Each member's
/// numeric value is fixed, so a stored integer keeps its meaning.
/// </summary>
public enum UxidErrorCode
{
    /// <summary>
    /// <c>compact_time_out_of_range</c>: a compact time whose first character
    /// would be the deterministic marker (the low 40 bits of the time at or
    /// above 31 x 2^35).
    /// </summary>
    CompactTimeOutOfRange = 0,

    /// <summary>
    /// <c>rand_width_mismatch</c>: fixed random bytes whose count is not the
    /// request's random field width.
    /// </summary>
    RandWidthMismatch = 1,

    /// <summary>
    /// <c>rand_size_too_large</c>: an explicit random field width above 10
    /// bytes.
    /// </summary>
    RandSizeTooLarge = 2,

    /// <summary>
    /// <c>from_with_monotonic</c>: a deterministic id asked for with
    /// monotonic generation.
    /// </summary>
    FromWithMonotonic = 3,

    /// <summary>
    /// <c>rand_with_from</c>: fixed random bytes asked for with a
    /// deterministic id.
    /// </summary>
    RandWithFrom = 4,

    /// <summary>
    /// <c>rand_with_monotonic</c>: fixed random bytes asked for with
    /// monotonic generation.
    /// </summary>
    RandWithMonotonic = 5,
}
