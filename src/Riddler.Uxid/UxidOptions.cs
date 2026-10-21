using System;
using System.Collections.Generic;

namespace Riddler;

/// <summary>
/// The options <see cref="Uxid.Generate(UxidOptions?)"/> builds an id from.
/// Every property is set at construction, and a property left unset takes
/// the default it names: a time-based id with no prefix, size
/// <see cref="UxidSize.Xl"/>, the size's random field width, standard time,
/// lower case, the delimiter <c>_</c>, the current time and random bytes
/// from <see cref="System.Security.Cryptography.RandomNumberGenerator"/>.
/// </summary>
public sealed class UxidOptions
{
    /// <summary>
    /// Gets the resource prefix, written before the delimiter exactly as given
    /// (case never applies to it). It may contain the delimiter. The empty
    /// string, the default, is no prefix: the id is the bare body, with no
    /// delimiter. A null prefix is refused with
    /// <see cref="ArgumentException"/>.
    /// </summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>
    /// Gets the size, which fixes the random field's width and so the body
    /// length. The default is <see cref="UxidSize.Xl"/>.
    /// </summary>
    public UxidSize Size { get; init; } = UxidSize.Xl;

    /// <summary>
    /// Gets an explicit random field width in bytes, from 0 to 10, in place of
    /// the size's width. Null, the default, takes the size's width. Above 10
    /// is refused with <see cref="UxidErrorCode.RandSizeTooLarge"/>, and
    /// below 0 with <see cref="ArgumentOutOfRangeException"/>. A body of a
    /// width the size table does not list has the same layout, but its length
    /// is one a decoder refuses.
    /// </summary>
    public int? RandSize { get; init; }

    /// <summary>
    /// Gets whether the time field is compact: the low 40 bits of the time in
    /// 8 characters, in place of the 48-bit standard time in 10, the freed 8
    /// bits going to the random field at every size but
    /// <see cref="UxidSize.Xl"/>. Null, the default, is standard time. A
    /// compact time whose first character would be the deterministic marker
    /// (from mid-2038 until the 40-bit window rolls over in 2039) is refused
    /// with <see cref="UxidErrorCode.CompactTimeOutOfRange"/>.
    /// </summary>
    public bool? CompactTime { get; init; }

    /// <summary>
    /// Gets the letter case of the body. The default is
    /// <see cref="UxidCase.Lower"/>. Case never applies to the prefix.
    /// </summary>
    public UxidCase Case { get; init; } = UxidCase.Lower;

    /// <summary>
    /// Gets the delimiter written between the prefix and the body, a non-empty
    /// string; the default is <c>_</c>. It is not recorded in the id, so a
    /// reader must be told the same one. An empty or null delimiter is refused
    /// with <see cref="ArgumentException"/>.
    /// </summary>
    public string Delimiter { get; init; } = "_";

    /// <summary>
    /// Gets the time the id carries, in milliseconds since the Unix epoch,
    /// from 0 to 2^48 - 1. Null, the default, reads the system clock in UTC
    /// at the call. A time outside that range is refused with
    /// <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <remarks>
    /// The time is a count of milliseconds rather than a
    /// <see cref="DateTimeOffset"/> because the time field is one: the count
    /// is exact, with no sub-millisecond part to drop, and it reaches the
    /// field's last value, 2^48 - 1, which lies beyond
    /// <see cref="DateTimeOffset.MaxValue"/>.
    /// <see cref="DateTimeOffset.ToUnixTimeMilliseconds()"/> converts one.
    /// </remarks>
    public long? Time { get; init; }

    /// <summary>
    /// Gets the source of the random field's bytes: a function that is given
    /// a count of bytes and returns an array of exactly that many. Null, the
    /// default, reads them from
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>. It is
    /// called once per id, and not at all when the random field is empty or
    /// <see cref="Rand"/> is set. A source that returns null or an array of
    /// another length is refused with <see cref="InvalidOperationException"/>;
    /// an exception the source throws reaches the caller unchanged.
    /// </summary>
    /// <remarks>
    /// The bytes are the id's only unpredictable part: a source passed here
    /// must be cryptographically secure for any id that must not be guessed.
    /// </remarks>
    public Func<int, byte[]>? ByteSource { get; init; }

    /// <summary>
    /// Gets fixed bytes for the random field, so that a fixed
    /// <see cref="Time"/> and fixed bytes always build the same id. They
    /// exist for reproducible vectors, such as the conformance corpus and
    /// tests, and are never a source of randomness: the id is exactly as
    /// guessable as the bytes passed. Their count must equal the random field
    /// width (the size's, or <see cref="RandSize"/>), or the request is
    /// refused with <see cref="UxidErrorCode.RandWidthMismatch"/>; size
    /// <see cref="UxidSize.Xs"/> in standard time has no random field, so
    /// only an empty list fits it. Null, the default, draws the bytes from
    /// <see cref="ByteSource"/>. The bytes are read when the id is built.
    /// </summary>
    public IReadOnlyList<byte>? Rand { get; init; }
}
