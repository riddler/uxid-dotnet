using System;
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
    private static int RandBytes(UxidSize size, bool compact) => size switch
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
    private static byte[] Draw(Func<int, byte[]>? source, int count)
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
}
