namespace Riddler;

/// <summary>
/// What a well-formed id says about itself, as
/// <see cref="Uxid.Decode(string, UxidDecodeOptions?)"/> reads it: its
/// prefix, the size and time form its body length gives, whether its body is
/// deterministic, and the time it carries. The random field and the hash
/// characters are not read back. Two values are equal when every property
/// is.
/// </summary>
public sealed record UxidDecoded
{
    internal UxidDecoded(string? prefix, UxidSize size, bool isCompact, bool isDeterministic, long? time)
    {
        Prefix = prefix;
        Size = size;
        IsCompact = isCompact;
        IsDeterministic = isDeterministic;
        Time = time;
    }

    /// <summary>
    /// Gets everything before the delimiter the body was split at, or null
    /// when the string has no delimiter. A string that starts with the
    /// delimiter, which no producer emits, has the empty prefix, best read as
    /// no prefix.
    /// </summary>
    public string? Prefix { get; }

    /// <summary>Gets the size the body length gives.</summary>
    public UxidSize Size { get; }

    /// <summary>
    /// Gets whether the body length is a compact-time one (13, 21 or 24).
    /// The lengths 10 and 18 are always read as standard time, so a compact
    /// <see cref="UxidSize.Xs"/> or compact <see cref="UxidSize.M"/> id
    /// decodes with this false and a time that is not the one it was
    /// generated at.
    /// </summary>
    public bool IsCompact { get; }

    /// <summary>
    /// Gets whether the body starts with the deterministic marker <c>z</c> or
    /// <c>Z</c>.
    /// </summary>
    public bool IsDeterministic { get; }

    /// <summary>
    /// Gets the time in milliseconds since the Unix epoch, or null for a
    /// deterministic body, which carries none. A compact time's high bits
    /// come from the decoder's clock (see
    /// <see cref="UxidDecodeOptions.Clock"/>), and the time is negative when
    /// that clock is within 2^40 milliseconds of the Unix epoch and the
    /// compact time is more than one day ahead of it.
    /// </summary>
    public long? Time { get; }
}
