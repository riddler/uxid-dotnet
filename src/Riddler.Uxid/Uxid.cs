using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Riddler;

/// <summary>
/// Prefixed, K-sortable identifiers a person can read, wire-compatible with
/// the Elixir uxid package: an optional prefix and delimiter, then a body in
/// Crockford Base32, either a millisecond time and a random field or, for a
/// deterministic id, a marker and a hash, as uxid's wire-format
/// specification lays it out.
/// </summary>
public static class Uxid
{
    /// <summary>The latest time a standard time field holds: 2^48 - 1 milliseconds.</summary>
    private const long _maxTime = (1L << 48) - 1;

    /// <summary>The low 40 bits a compact time field holds.</summary>
    private const long _compactMask = (1L << 40) - 1;

    /// <summary>The widest random field at any size, in bytes.</summary>
    private const int _maxRandBytes = 10;

    /// <summary>The deterministic marker's value, which a compact time may not start with.</summary>
    private const long _marker = 31;

    /// <summary>The width of a compact time's window, 2^40 milliseconds.</summary>
    private const long _compactWindow = 1L << 40;

    /// <summary>
    /// How far ahead of the decoder's clock a compact time may land before it
    /// is read as one from the window before: one day in milliseconds.
    /// </summary>
    private const long _oneDay = 86400000;

    /// <summary>
    /// Generates an id: the prefix and the delimiter (none for no prefix),
    /// then the time field and the random field; or, when
    /// <see cref="UxidOptions.From"/> is set, the deterministic body derived
    /// from it, for which no clock and no byte source is read.
    /// </summary>
    /// <param name="options">
    /// The options to build the id from; null takes every default (see
    /// <see cref="UxidOptions"/>).
    /// </param>
    /// <returns>The id, for example <c>usr_01hf7yat0004hmasw9nf6yy093</c>.</returns>
    /// <exception cref="UxidException">
    /// The request is one the wire format refuses:
    /// <see cref="UxidErrorCode.RandSizeTooLarge"/>,
    /// <see cref="UxidErrorCode.RandWidthMismatch"/>,
    /// <see cref="UxidErrorCode.CompactTimeOutOfRange"/> or
    /// <see cref="UxidErrorCode.RandWithFrom"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The prefix is null, the delimiter is null or empty, or, for a
    /// deterministic id, the prefix or <see cref="UxidOptions.From"/> is not
    /// well-formed UTF-16 (a lone surrogate).
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size or case is not one of its enum's members, the random field
    /// width is negative, or the time (or the system clock) is outside 0 to
    /// 2^48 - 1 milliseconds.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The byte source returned null or an array of another length than it
    /// was asked for.
    /// </exception>
    public static string Generate(UxidOptions? options = null)
    {
        options ??= new UxidOptions();

        var prefix = options.Prefix
            ?? throw new ArgumentException("The prefix must not be null; the empty string is no prefix.", nameof(options));
        var delimiter = options.Delimiter;
        if (string.IsNullOrEmpty(delimiter))
        {
            throw new ArgumentException("The delimiter must be a non-empty string.", nameof(options));
        }

        if (options.Size is not (UxidSize.Xs or UxidSize.S or UxidSize.M or UxidSize.L or UxidSize.Xl))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Size, "The size is not a UxidSize member.");
        }

        if (options.Case is not (UxidCase.Lower or UxidCase.Upper))
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Case, "The case is not a UxidCase member.");
        }

        if (options.From is { } from)
        {
            if (options.Rand is not null)
            {
                throw new UxidException(
                    UxidErrorCode.RandWithFrom,
                    "Fixed random bytes cannot be used with From: a deterministic id has no random field.");
            }

            try
            {
                return Deterministic(prefix, delimiter, from, options.Size, options.Case);
            }
            catch (EncoderFallbackException malformed)
            {
                throw new ArgumentException(
                    "The prefix and From must be well-formed UTF-16 to be hashed as UTF-8; a lone surrogate has no UTF-8 bytes.",
                    nameof(options),
                    malformed);
            }
        }

        var time = options.Time ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (time is < 0 or > _maxTime)
        {
            throw new ArgumentOutOfRangeException(nameof(options), time, "The time must be from 0 to 2^48 - 1 milliseconds since the Unix epoch.");
        }

        var compact = options.CompactTime == true;
        var width = RandBytes(options.Size, compact);
        if (options.RandSize is int randSize)
        {
            if (randSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), randSize, "The random field width must not be negative.");
            }

            if (randSize > _maxRandBytes)
            {
                throw new UxidException(
                    UxidErrorCode.RandSizeTooLarge,
                    "The random field width must be at most 10 bytes, got " + randSize + ".");
            }

            width = randSize;
        }

        if (options.Rand is { } fixedRand && fixedRand.Count != width)
        {
            throw new UxidException(
                UxidErrorCode.RandWidthMismatch,
                "The fixed random bytes must be exactly " + width + " bytes for this request, got " + fixedRand.Count + ".");
        }

        var compactTime = time & _compactMask;
        if (compact && compactTime >> 35 == _marker)
        {
            throw new UxidException(
                UxidErrorCode.CompactTimeOutOfRange,
                "A compact time whose first character would be the deterministic marker cannot be encoded; use standard time.");
        }

        var rand = options.Rand ?? Draw(options.ByteSource, width);

        var id = new StringBuilder(prefix.Length + delimiter.Length + 26);
        if (prefix.Length > 0)
        {
            id.Append(prefix).Append(delimiter);
        }

        if (compact)
        {
            Base32.AppendInteger(id, compactTime, 8, options.Case);
        }
        else
        {
            Base32.AppendInteger(id, time, 10, options.Case);
        }

        Base32.AppendBytes(id, rand, options.Case);
        return id.ToString();
    }

    /// <summary>
    /// Reads a string back to what it says about itself: its prefix, its
    /// size and time form, whether it is deterministic, and its time. Any
    /// string gets an answer: a malformed one is refused in the result, never
    /// with an exception.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The body is everything after the last delimiter found reading left to
    /// right, matches not overlapping (under <c>--</c>,
    /// <c>usr---01hf7yat00</c> splits after <c>usr</c>), and the prefix is
    /// everything before it; a string with no delimiter is a body with no
    /// prefix. The body's length in UTF-8 bytes gives the size and the time
    /// form: 10 <see cref="UxidSize.Xs"/>, 14 <see cref="UxidSize.S"/>,
    /// 18 <see cref="UxidSize.M"/>, 22 <see cref="UxidSize.L"/> and 26
    /// <see cref="UxidSize.Xl"/> in standard time; 13 <see cref="UxidSize.S"/>,
    /// 21 <see cref="UxidSize.L"/> and 24 <see cref="UxidSize.Xl"/> in
    /// compact time. The lengths 10 and 18 are also those of a compact
    /// <see cref="UxidSize.Xs"/> and a compact <see cref="UxidSize.M"/> body,
    /// and they are always read as standard time: such an id decodes to its
    /// size, not compact, with a time read from its first 10 characters,
    /// which is not the time it was generated at.
    /// </para>
    /// <para>
    /// A body is refused for the first rule it breaks, in this order:
    /// <see cref="UxidDecodeError.Empty"/> (an empty string, or one ending in
    /// the delimiter), <see cref="UxidDecodeError.InvalidLength"/> (a length
    /// in bytes not listed above) and
    /// <see cref="UxidDecodeError.InvalidCharacter"/> (a character that is
    /// not a Crockford Base32 symbol in either case). A body starting with
    /// <c>z</c> or <c>Z</c> is deterministic and carries no time; a compact
    /// time takes its high bits from <see cref="UxidDecodeOptions.Clock"/>.
    /// </para>
    /// </remarks>
    /// <param name="id">The string to read.</param>
    /// <param name="options">
    /// The delimiter and the clock to read it with; null takes the defaults
    /// (see <see cref="UxidDecodeOptions"/>).
    /// </param>
    /// <returns>
    /// The decoded value, for example prefix <c>usr</c>, size
    /// <see cref="UxidSize.S"/>, standard time and the time 1700000000000
    /// for <c>usr_01hf7yat0004h1</c>; or the refusal reason.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is null.</exception>
    /// <exception cref="ArgumentException">The delimiter is null or empty.</exception>
    public static UxidDecodeResult Decode(string id, UxidDecodeOptions? options = null)
    {
        // netstandard2.0 has no ArgumentNullException.ThrowIfNull.
#if NET6_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(id);
#else
        if (id is null)
        {
            throw new ArgumentNullException(nameof(id));
        }
#endif

        options ??= new UxidDecodeOptions();
        var delimiter = CheckDelimiter(options.Delimiter, nameof(options));
        var body = Split(id, delimiter, out var prefix);
        if (Refusal(body, out var size, out var compact) is { } refusal)
        {
            return UxidDecodeResult.Refused(refusal);
        }

        var deterministic = body[0] is 'z' or 'Z';
        long? time = null;
        if (!deterministic)
        {
            time = compact
                ? CompactTime(Base32.ReadInteger(body, 0, 8), options.Clock)
                : ((Base32.ReadInteger(body, 0, 1) & 7) << 45) | Base32.ReadInteger(body, 1, 9);
        }

        return UxidDecodeResult.Decoded(new UxidDecoded(prefix, size, compact, deterministic, time));
    }

    /// <summary>
    /// Reads a string as <see cref="Decode(string, UxidDecodeOptions?)"/>
    /// does with the default options (the delimiter <c>_</c> and the system
    /// clock), answering whether it decoded.
    /// </summary>
    /// <param name="id">The string to read; null is refused.</param>
    /// <param name="decoded">
    /// The decoded value when the method returns true; null otherwise.
    /// </param>
    /// <returns>True when the string decoded; false when it was refused or is null.</returns>
    public static bool TryDecode(string? id, [NotNullWhen(true)] out UxidDecoded? decoded)
    {
        decoded = id is null ? null : Decode(id).Value;
        return decoded is not null;
    }

    /// <summary>
    /// Answers whether a string is a well-formed id under a delimiter, the
    /// one <see cref="Decode(string, UxidDecodeOptions?)"/> would decode,
    /// and, given a required prefix, whether it carries exactly that prefix.
    /// This checks structure, not origin: any well-formed string passes. It
    /// reads no clock.
    /// </summary>
    /// <param name="id">The string to check; null is not valid.</param>
    /// <param name="delimiter">The delimiter the id was generated with, a non-empty string.</param>
    /// <param name="requiredPrefix">
    /// The prefix the id must carry, compared ordinally; null or the empty
    /// string, the default, accepts any prefix or none. An id with no
    /// delimiter has no prefix, so it fails any other required prefix.
    /// </param>
    /// <returns>True when the id is well formed and carries the required prefix.</returns>
    /// <exception cref="ArgumentException"><paramref name="delimiter"/> is null or empty.</exception>
    public static bool IsValid(string? id, string delimiter, string? requiredPrefix = null)
    {
        CheckDelimiter(delimiter, nameof(delimiter));
        if (id is null)
        {
            return false;
        }

        var body = Split(id, delimiter, out var prefix);
        return Refusal(body, out _, out _) is null
            && (string.IsNullOrEmpty(requiredPrefix) || string.Equals(prefix, requiredPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Answers whether a string is a well-formed deterministic id under a
    /// delimiter: one <see cref="Decode(string, UxidDecodeOptions?)"/> would
    /// decode whose body starts with the marker <c>z</c> or <c>Z</c>. A
    /// malformed string is not one, whatever its first body character. It
    /// reads no clock.
    /// </summary>
    /// <param name="id">The string to check; null is not deterministic.</param>
    /// <param name="delimiter">The delimiter the id was generated with, a non-empty string.</param>
    /// <returns>True when the id is well formed and deterministic.</returns>
    /// <exception cref="ArgumentException"><paramref name="delimiter"/> is null or empty.</exception>
    public static bool IsDeterministic(string? id, string delimiter)
    {
        CheckDelimiter(delimiter, nameof(delimiter));
        if (id is null)
        {
            return false;
        }

        var body = Split(id, delimiter, out _);
        return Refusal(body, out _, out _) is null && body[0] is 'z' or 'Z';
    }

    /// <summary>
    /// Reads a size from its short spelling (<c>xs</c>, <c>s</c>, <c>m</c>,
    /// <c>l</c>, <c>xl</c>) or its long one (<c>xsmall</c>, <c>small</c>,
    /// <c>medium</c>, <c>large</c>, <c>xlarge</c>), in lower case exactly.
    /// </summary>
    /// <param name="text">The spelling to read.</param>
    /// <returns>The size it names.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is no size's spelling.</exception>
    public static UxidSize ParseSize(string text)
    {
        return text switch
        {
            null => throw new ArgumentNullException(nameof(text)),
            "xs" or "xsmall" => UxidSize.Xs,
            "s" or "small" => UxidSize.S,
            "m" or "medium" => UxidSize.M,
            "l" or "large" => UxidSize.L,
            "xl" or "xlarge" => UxidSize.Xl,
            _ => throw new ArgumentException("Not a size: expected xs, s, m, l, xl, xsmall, small, medium, large or xlarge.", nameof(text)),
        };
    }

    /// <summary>
    /// The random field's width in bytes for a size and time form; the caller
    /// has checked that the size is a member, so the last arm is
    /// <see cref="UxidSize.Xl"/>.
    /// </summary>
    internal static int RandBytes(UxidSize size, bool compact) => size switch
    {
        UxidSize.Xs => compact ? 1 : 0,
        UxidSize.S => compact ? 3 : 2,
        UxidSize.M => compact ? 6 : 5,
        UxidSize.L => compact ? 8 : 7,
        _ => 10,
    };

    /// <summary>
    /// The number of hash characters a deterministic body carries at a size,
    /// one fewer than the standard body length; the caller has checked that
    /// the size is a member, so the last arm is <see cref="UxidSize.Xl"/>.
    /// </summary>
    private static int HashChars(UxidSize size) => size switch
    {
        UxidSize.Xs => 9,
        UxidSize.S => 13,
        UxidSize.M => 17,
        UxidSize.L => 21,
        _ => 25,
    };

    /// <summary>
    /// Builds a deterministic id: the prefix and the delimiter (none for no
    /// prefix), the marker, then the first 5 bits per hash character of the
    /// SHA-256 digest of the prefix's UTF-8 bytes, a zero byte, the string's
    /// UTF-8 bytes, a zero byte and the count of hash characters as one byte.
    /// </summary>
    /// <exception cref="EncoderFallbackException">
    /// The prefix or the string holds a lone surrogate.
    /// </exception>
    private static string Deterministic(string prefix, string delimiter, string from, UxidSize size, UxidCase letterCase)
    {
        var count = HashChars(size);
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var prefixBytes = utf8.GetBytes(prefix);
        var fromBytes = utf8.GetBytes(from);

        var input = new byte[prefixBytes.Length + 1 + fromBytes.Length + 1 + 1];
        Buffer.BlockCopy(prefixBytes, 0, input, 0, prefixBytes.Length);
        Buffer.BlockCopy(fromBytes, 0, input, prefixBytes.Length + 1, fromBytes.Length);
        input[input.Length - 1] = (byte)count;

        // netstandard2.0 has no static SHA256.HashData; there the hash object
        // is created and disposed for this one digest.
#if NET5_0_OR_GREATER
        var digest = SHA256.HashData(input);
#else
        byte[] digest;
        using (var sha256 = SHA256.Create())
        {
            digest = sha256.ComputeHash(input);
        }
#endif

        var id = new StringBuilder(prefix.Length + delimiter.Length + 1 + count);
        if (prefix.Length > 0)
        {
            id.Append(prefix).Append(delimiter);
        }

        id.Append(letterCase == UxidCase.Upper ? 'Z' : 'z');
        Base32.AppendLeadingBits(id, digest, count, letterCase);
        return id.ToString();
    }

    /// <summary>
    /// Draws <paramref name="count"/> bytes from the caller's source, or from
    /// <see cref="RandomNumberGenerator"/> when there is none. An empty field
    /// draws nothing.
    /// </summary>
    internal static byte[] Draw(Func<int, byte[]>? source, int count)
    {
        if (count == 0)
        {
            return [];
        }

        if (source is null)
        {
            var bytes = new byte[count];
            using (var generator = RandomNumberGenerator.Create())
            {
                generator.GetBytes(bytes);
            }

            return bytes;
        }

        var drawn = source(count)
            ?? throw new InvalidOperationException("The byte source returned null.");
        if (drawn.Length != count)
        {
            throw new InvalidOperationException(
                "The byte source returned " + drawn.Length + " bytes when asked for " + count + ".");
        }

        return drawn;
    }

    /// <summary>Returns a delimiter that is set and non-empty, or throws.</summary>
    private static string CheckDelimiter(string? delimiter, string parameter)
    {
        if (string.IsNullOrEmpty(delimiter))
        {
            throw new ArgumentException("The delimiter must be a non-empty string.", parameter);
        }

        return delimiter!;
    }

    /// <summary>
    /// Splits an id at the last delimiter found by a left-to-right scan whose
    /// matches do not overlap, returning the body; the prefix is everything
    /// before that delimiter, or null when there is none.
    /// </summary>
    private static string Split(string id, string delimiter, out string? prefix)
    {
        var last = -1;
        var from = 0;
        int found;
        while ((found = id.IndexOf(delimiter, from, StringComparison.Ordinal)) >= 0)
        {
            last = found;
            from = found + delimiter.Length;
        }

        if (last < 0)
        {
            prefix = null;
            return id;
        }

        prefix = id.Substring(0, last);
        return id.Substring(last + delimiter.Length);
    }

    /// <summary>
    /// The first rule a body breaks, in the order empty, length in UTF-8
    /// bytes, alphabet; or null for a well-formed body, with the size and
    /// time form its length gives.
    /// </summary>
    private static UxidDecodeError? Refusal(string body, out UxidSize size, out bool compact)
    {
        compact = false;
        size = UxidSize.Xl;
        if (body.Length == 0)
        {
            return UxidDecodeError.Empty;
        }

        switch (Encoding.UTF8.GetByteCount(body))
        {
            case 10: size = UxidSize.Xs; break;
            case 13: size = UxidSize.S; compact = true; break;
            case 14: size = UxidSize.S; break;
            case 18: size = UxidSize.M; break;
            case 21: size = UxidSize.L; compact = true; break;
            case 22: size = UxidSize.L; break;
            case 24: compact = true; break;
            case 26: break;
            default: return UxidDecodeError.InvalidLength;
        }

        foreach (var symbol in body)
        {
            if (Base32.Value(symbol) < 0)
            {
                return UxidDecodeError.InvalidCharacter;
            }
        }

        return null;
    }

    /// <summary>
    /// The full time of a compact body: its low 40 bits under the clock's
    /// high bits, less one 2^40 ms window when that lands more than one day
    /// ahead of the clock. There is no floor at zero, so a clock in the first
    /// window can give a negative time.
    /// </summary>
    private static long CompactTime(long low, Func<long>? clock)
    {
        var now = clock is null ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : clock();
        var time = (now & ~_compactMask) | low;
        return time - now > _oneDay ? time - _compactWindow : time;
    }
}
