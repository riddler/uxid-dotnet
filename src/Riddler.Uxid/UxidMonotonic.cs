using System;
using System.Text;

namespace Riddler;

/// <summary>
/// A monotonic generator: the ids it makes increase strictly, in ordinal
/// string order as in time order, also when several fall in one millisecond
/// and when the clock moves back. It follows the "Monotonic generation" rule
/// of uxid's wire-format specification. A monotonic id is an ordinary
/// time-based id of its size and length: nothing in it says how it was made,
/// and <see cref="Uxid.Decode(string, UxidDecodeOptions?)"/> reads it as any
/// other.
/// </summary>
/// <remarks>
/// <para>
/// A generator is built for one prefix and one random field width, so its
/// state is kept per prefix and random width: the last time <c>T</c> it gave
/// and the last random value <c>R</c>, the random field read as one
/// big-endian unsigned integer of <c>w</c> bytes. For a call at time
/// <c>now</c>:
/// </para>
/// <list type="number">
/// <item><description>
/// with no last id, or <c>now</c> later than <c>T</c>, the id carries
/// <c>now</c> and a fresh random value, the seed;
/// </description></item>
/// <item><description>
/// with <c>now</c> equal to or earlier than <c>T</c>, the id carries
/// <c>T</c> and <c>R + step</c>, where the step is drawn uniformly from 1 to
/// 2^(4w) inclusive, half the field's bit width;
/// </description></item>
/// <item><description>
/// when <c>R + step</c> is 2^(8w) or more, the id carries <c>T + 1</c> and a
/// fresh seed instead.
/// </description></item>
/// </list>
/// <para>
/// Without an <see cref="IUxidDraws"/>, both values come from the byte
/// source (<see cref="UxidOptions.ByteSource"/>, or
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/>): a seed
/// is <c>w</c> bytes; a step is the low <c>4w</c> bits of <c>ceil(w / 2)</c>
/// bytes, read big-endian, plus one. That range holds a power of two values,
/// so the masked bits are exactly uniform over it, with no modulo bias and
/// nothing redrawn.
/// </para>
/// <para>
/// The step is not a plus-one counter: consecutive ids in a millisecond are
/// spread over a window of 2^(4w) values. That stops trivial enumeration but
/// is a mitigation, not unpredictability, and a small random field stays
/// small: a standard <see cref="UxidSize.S"/> id has 16 random bits. A
/// compact time holds only the low 40 bits of the time, so compact ids keep
/// their order only within one 2^40 millisecond window, as every compact id
/// does.
/// </para>
/// <para>
/// <b>Safe for concurrent use.</b> One generator may be called from several
/// threads at once: a private lock object inside the instance orders the
/// calls, so its ids increase strictly in the order the calls take the lock,
/// whichever thread asks. The byte source and the draws are called with that
/// lock held. The state lives in the instance alone, nothing is kept in a
/// static field, and two generators never affect each other: ids from two
/// generators carry no order between them within a millisecond.
/// </para>
/// <para>
/// <b>A refused call leaves the generator as it was.</b> When
/// <see cref="Generate()"/> throws, the last id's time and random value stay
/// what they were, and the next call starts from them; any value drawn for
/// the refused call is discarded. So after an overflow onto a refused compact
/// time, a later call at or before the last id's time draws a new step: it is
/// refused again if that step also overflows the field, and with a smaller
/// step that does not, it returns an id in the last id's millisecond.
/// </para>
/// </remarks>
public sealed class UxidMonotonic
{
    /// <summary>The latest time a time field holds: 2^48 - 1 milliseconds.</summary>
    private const long _maxTime = (1L << 48) - 1;

    /// <summary>The low 40 bits a compact time field holds.</summary>
    private const long _compactMask = (1L << 40) - 1;

    /// <summary>The widest random field, in bytes.</summary>
    private const int _maxRandBytes = 10;

    /// <summary>The deterministic marker's value, which a compact time may not start with.</summary>
    private const long _marker = 31;

    /// <summary>The lock every call takes, private to this instance.</summary>
    private readonly object _gate = new();

    private readonly string _head;
    private readonly UxidCase _case;
    private readonly bool _compact;
    private readonly int _width;
    private readonly long? _time;
    private readonly Func<int, byte[]>? _byteSource;
    private readonly IUxidDraws? _draws;

    /// <summary>Whether a call has returned an id yet.</summary>
    private bool _started;

    /// <summary>The time the last id carries.</summary>
    private long _lastTime;

    /// <summary>The last id's random field, most significant byte first.</summary>
    private byte[] _last = [];

    /// <summary>
    /// Builds a monotonic generator from <paramref name="options"/>: its
    /// <see cref="UxidOptions.Prefix"/>, <see cref="UxidOptions.Size"/>,
    /// <see cref="UxidOptions.RandSize"/>, <see cref="UxidOptions.CompactTime"/>,
    /// <see cref="UxidOptions.Case"/>, <see cref="UxidOptions.Delimiter"/>,
    /// <see cref="UxidOptions.Time"/> and <see cref="UxidOptions.ByteSource"/>,
    /// each read as <see cref="Uxid.Generate(UxidOptions?)"/> reads it, with
    /// the same defaults, but one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one default that differs: an unset
    /// <see cref="UxidOptions.CompactTime"/> is compact time at
    /// <see cref="UxidSize.Xs"/> and standard time at every other size,
    /// because a standard <see cref="UxidSize.Xs"/> has no random field to
    /// step and a compact one has a one-byte field. The random field width
    /// (the size's, or <see cref="UxidOptions.RandSize"/>) must be at least
    /// one byte, so an <see cref="UxidSize.Xs"/> with
    /// <see cref="UxidOptions.CompactTime"/> false and no
    /// <see cref="UxidOptions.RandSize"/> is refused, and so is a
    /// <see cref="UxidOptions.RandSize"/> of 0.
    /// </para>
    /// <para>
    /// A set <see cref="UxidOptions.Time"/> is the time of every call that
    /// names none; the ids still increase, stepping within that millisecond
    /// and moving past it on overflow.
    /// </para>
    /// <para>
    /// <see cref="UxidOptions.From"/> and <see cref="UxidOptions.Rand"/> are
    /// always refused, here rather than at each call, so a generator that
    /// exists can make ids: a monotonic generator makes neither a
    /// deterministic id nor one with a fixed random field.
    /// </para>
    /// </remarks>
    /// <param name="options">
    /// The options the ids are built from; null takes every default (see
    /// <see cref="UxidOptions"/>).
    /// </param>
    /// <param name="draws">
    /// The seeds and steps themselves, in place of the byte source, which is
    /// then never called; null, the default, draws them from the byte
    /// source. It exists to replay known values, such as the conformance
    /// corpus's, never as a source of randomness.
    /// </param>
    /// <exception cref="UxidException">
    /// The options are refused, checked in this order:
    /// <see cref="UxidErrorCode.FromWithMonotonic"/> for a set
    /// <see cref="UxidOptions.From"/>,
    /// <see cref="UxidErrorCode.RandWithMonotonic"/> for a set
    /// <see cref="UxidOptions.Rand"/>, and, after the checks
    /// <see cref="Uxid.Generate(UxidOptions?)"/> makes of the prefix, the
    /// delimiter, the size and the case,
    /// <see cref="UxidErrorCode.RandSizeTooLarge"/> for a
    /// <see cref="UxidOptions.RandSize"/> above 10.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The prefix is null, the delimiter is null or empty, or the random
    /// field width is 0 bytes.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size or case is not one of its enum's members, the random field
    /// width is negative, or <see cref="UxidOptions.Time"/> is outside 0 to
    /// 2^48 - 1 milliseconds.
    /// </exception>
    public UxidMonotonic(UxidOptions? options = null, IUxidDraws? draws = null)
    {
        options ??= new UxidOptions();
        if (options.From is not null)
        {
            throw new UxidException(
                UxidErrorCode.FromWithMonotonic,
                "From cannot be used with a monotonic generator: a deterministic id has no random field to step.");
        }

        if (options.Rand is not null)
        {
            throw new UxidException(
                UxidErrorCode.RandWithMonotonic,
                "Fixed random bytes cannot be used with a monotonic generator: the generator chooses the random field.");
        }

        var prefix = options.Prefix
            ?? throw new ArgumentException("The prefix must not be null; the empty string is no prefix.", nameof(options));
        if (string.IsNullOrEmpty(options.Delimiter))
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

        var compact = options.CompactTime ?? options.Size == UxidSize.Xs;
        var width = Uxid.RandBytes(options.Size, compact);
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

        if (width == 0)
        {
            throw new ArgumentException(
                "A monotonic id needs a random field to step, but this random field width is 0 bytes; leave CompactTime unset at size Xs, or set a RandSize from 1 to 10.",
                nameof(options));
        }

        if (options.Time is < 0 or > _maxTime)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.Time, "The time must be from 0 to 2^48 - 1 milliseconds since the Unix epoch.");
        }

        _head = prefix.Length > 0 ? prefix + options.Delimiter : string.Empty;
        _case = options.Case;
        _compact = compact;
        _width = width;
        _time = options.Time;
        _byteSource = options.ByteSource;
        _draws = draws;
    }

    /// <summary>
    /// Generates the next id at the time of the options the generator was
    /// built from, or, when that is unset, at the system clock in UTC, under
    /// the rule the class describes.
    /// </summary>
    /// <returns>The id, for example <c>usr_01hf7yat0000000000003nqk8n</c>.</returns>
    /// <exception cref="UxidException">
    /// <see cref="UxidErrorCode.CompactTimeOutOfRange"/>: the compact time
    /// the id would carry starts with the deterministic marker, also when
    /// that time is the millisecond after the last id's, reached by an
    /// overflow.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The system clock is outside 0 to 2^48 - 1 milliseconds, or a step
    /// overflows the random field in millisecond 2^48 - 1, the last a time
    /// field holds.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The byte source or a seed draw returned null or an array of another
    /// length than the random field width, or a step draw is outside 1 to
    /// 2^(4w).
    /// </exception>
    public string Generate() => Generate(_time ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    /// <summary>
    /// Generates the next id at <paramref name="time"/>, under the rule the
    /// class describes: the id carries this time, the last id's time, or the
    /// millisecond after it. Giving the time is how a caller fixes the clock.
    /// </summary>
    /// <param name="time">
    /// The current time in milliseconds since the Unix epoch, from 0 to
    /// 2^48 - 1.
    /// </param>
    /// <returns>The id, for example <c>usr_01hf7yat0000000000003nqk8n</c>.</returns>
    /// <exception cref="UxidException">
    /// <see cref="UxidErrorCode.CompactTimeOutOfRange"/>: the compact time
    /// the id would carry starts with the deterministic marker, also when
    /// that time is the millisecond after the last id's, reached by an
    /// overflow.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="time"/> is outside 0 to 2^48 - 1 milliseconds, or a
    /// step overflows the random field in millisecond 2^48 - 1, the last a
    /// time field holds.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The byte source or a seed draw returned null or an array of another
    /// length than the random field width, or a step draw is outside 1 to
    /// 2^(4w).
    /// </exception>
    public string Generate(long time)
    {
        if (time is < 0 or > _maxTime)
        {
            throw new ArgumentOutOfRangeException(nameof(time), time, "The time must be from 0 to 2^48 - 1 milliseconds since the Unix epoch.");
        }

        lock (_gate)
        {
            var at = time;
            byte[] field;
            if (_started && time <= _lastTime)
            {
                at = _lastTime;
                field = (byte[])_last.Clone();
                if (AddStep(field, Step()))
                {
                    if (at == _maxTime)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(time),
                            time,
                            "The random field overflowed in millisecond 2^48 - 1, the last a time field holds.");
                    }

                    at++;
                    field = Seed();
                }
            }
            else
            {
                field = Seed();
            }

            var compactTime = at & _compactMask;
            if (_compact && compactTime >> 35 == _marker)
            {
                throw new UxidException(
                    UxidErrorCode.CompactTimeOutOfRange,
                    "A compact time whose first character would be the deterministic marker cannot be encoded; use standard time.");
            }

            var id = new StringBuilder(_head.Length + 26);
            id.Append(_head);
            if (_compact)
            {
                Base32.AppendInteger(id, compactTime, 8, _case);
            }
            else
            {
                Base32.AppendInteger(id, at, 10, _case);
            }

            Base32.AppendBytes(id, field, _case);

            _started = true;
            _lastTime = at;
            _last = field;
            return id.ToString();
        }
    }

    /// <summary>
    /// Adds <paramref name="step"/> to <paramref name="field"/>, a big-endian
    /// unsigned integer, in place, and reports whether the sum overflowed the
    /// field's width.
    /// </summary>
    private static bool AddStep(byte[] field, long step)
    {
        var carry = step;
        for (var index = field.Length - 1; index >= 0 && carry > 0; index--)
        {
            var sum = field[index] + (carry & 0xff);
            field[index] = (byte)sum;
            carry = (carry >> 8) + (sum >> 8);
        }

        return carry > 0;
    }

    /// <summary>
    /// A fresh random field: from the draws when the generator has them,
    /// otherwise <c>w</c> bytes from the byte source.
    /// </summary>
    private byte[] Seed()
    {
        if (_draws is null)
        {
            return (byte[])Uxid.Draw(_byteSource, _width).Clone();
        }

        var seed = _draws.DrawSeed(_width)
            ?? throw new InvalidOperationException("The draws returned a null seed.");
        if (seed.Length != _width)
        {
            throw new InvalidOperationException(
                "The draws returned a seed of " + seed.Length + " bytes for a random field of " + _width + ".");
        }

        return (byte[])seed.Clone();
    }

    /// <summary>
    /// A step from 1 to 2^(4w): from the draws when the generator has them,
    /// otherwise the low <c>4w</c> bits of <c>ceil(w / 2)</c> bytes from the
    /// byte source, read big-endian, plus one.
    /// </summary>
    private long Step()
    {
        var bits = 4 * _width;
        if (_draws is not null)
        {
            var step = _draws.DrawStep(_width);
            if (step < 1 || step > 1L << bits)
            {
                throw new InvalidOperationException(
                    "The draws returned the step " + step + ", outside 1 to 2^" + bits + ".");
            }

            return step;
        }

        long value = 0;
        foreach (var drawn in Uxid.Draw(_byteSource, (_width + 1) / 2))
        {
            value = (value << 8) | drawn;
        }

        return (value & ((1L << bits) - 1)) + 1;
    }
}
