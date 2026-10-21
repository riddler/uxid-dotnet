namespace Riddler;

/// <summary>
/// Why a string was refused on decode: the closed set of refusal reasons the
/// wire format names. <see cref="UxidDecodeResult.ErrorName"/> spells each
/// one as the wire format and its conformance corpus do. A body that breaks
/// more than one rule is refused for the first it breaks, in the order of the
/// members below. Each member's numeric value is fixed, so a stored integer
/// keeps its meaning.
/// </summary>
public enum UxidDecodeError
{
    /// <summary>
    /// <c>empty</c>: the body is empty, because the string is empty or ends
    /// in the delimiter.
    /// </summary>
    Empty = 0,

    /// <summary>
    /// <c>invalid_length</c>: the body's length in UTF-8 bytes is not one of
    /// the body lengths a size gives (10, 13, 14, 18, 21, 22, 24 or 26).
    /// </summary>
    InvalidLength = 1,

    /// <summary>
    /// <c>invalid_character</c>: a body character is not a Crockford Base32
    /// symbol in either case. The letters <c>I</c>, <c>L</c>, <c>O</c> and
    /// <c>U</c> are not symbols, and no reading substitution is applied.
    /// </summary>
    InvalidCharacter = 2,
}
