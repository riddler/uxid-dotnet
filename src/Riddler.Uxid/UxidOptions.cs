using System;
using System.Collections.Generic;

namespace Riddler;

/// <summary>
/// The options <see cref="Uxid.Generate(UxidOptions?)"/> builds an id from.
/// Every property is set at construction, and a property left unset takes
/// the default it names: a time-based id (no <see cref="From"/>) with no
/// prefix, size
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
    /// length; for a deterministic id (<see cref="From"/>) it fixes the count
    /// of hash characters instead. The default is <see cref="UxidSize.Xl"/>.
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
    /// called once per time-based id, and not at all when the random field is
    /// empty, <see cref="Rand"/> is set or the id is deterministic
    /// (<see cref="From"/>). A source that returns null or an array of
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

    /// <summary>
    /// Gets the string a deterministic id is derived from: when set, the id's
    /// body is the marker <c>z</c> (<c>Z</c> in upper case) followed by a
    /// SHA-256 hash of the prefix and this string, so the same prefix, string
    /// and size always build the same id. Null, the default, builds a
    /// time-based id. The empty string is a string to derive from, not none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The hash input is the prefix's UTF-8 bytes (none for no prefix), a zero
    /// byte, this string's UTF-8 bytes, a zero byte and one byte holding the
    /// body's count of hash characters; the body is the marker and the first
    /// 5 bits per hash character of the digest. The size sets that count:
    /// <see cref="UxidSize.Xs"/> 9, <see cref="UxidSize.S"/> 13,
    /// <see cref="UxidSize.M"/> 17, <see cref="UxidSize.L"/> 21 and
    /// <see cref="UxidSize.Xl"/> 25, so the body has a standard body's length,
    /// and the same string at two sizes or under two prefixes gives unrelated
    /// bodies. <see cref="Case"/> and <see cref="Delimiter"/> apply as they do
    /// to a time-based id; case is not part of the hash input.
    /// </para>
    /// <para>
    /// A deterministic id carries no time and no random field, so
    /// <see cref="Time"/>, <see cref="CompactTime"/>, <see cref="RandSize"/>
    /// and <see cref="ByteSource"/> are not read (there is no compact
    /// deterministic form), and <see cref="Rand"/> set beside this property is
    /// refused with <see cref="UxidErrorCode.RandWithFrom"/>. A prefix or a
    /// string that is not well-formed UTF-16 (a lone surrogate) has no UTF-8
    /// bytes and is refused with <see cref="ArgumentException"/>.
    /// </para>
    /// <para>
    /// The id is exactly as guessable as the string: anyone who knows the
    /// prefix, the string and the size can build it.
    /// </para>
    /// </remarks>
    public string? From { get; init; }
}
