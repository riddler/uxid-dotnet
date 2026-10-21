using System;

namespace Riddler;

/// <summary>
/// The options <see cref="Uxid.Decode(string, UxidDecodeOptions?)"/> reads a
/// string with. Every property is set at construction, and a property left
/// unset takes the default it names: the delimiter <c>_</c> and the system
/// clock in UTC.
/// </summary>
public sealed class UxidDecodeOptions
{
    /// <summary>
    /// Gets the delimiter the string is split at, a non-empty string; the
    /// default is <c>_</c>. An id does not record its delimiter, so this must
    /// be the one it was generated with. An empty or null delimiter is
    /// refused with <see cref="ArgumentException"/>.
    /// </summary>
    public string Delimiter { get; init; } = "_";

    /// <summary>
    /// Gets the decoder's clock: a function returning the current time in
    /// milliseconds since the Unix epoch. A compact body carries only the low
    /// 40 bits of its time, and the high bits are this clock's; when the
    /// result is more than one day (86400000 ms) ahead of the clock, one
    /// window of 2^40 ms is subtracted, with no floor at zero. Null, the
    /// default, reads the system clock in UTC. It is called once per compact
    /// time-based body, and not at all for a standard or deterministic body
    /// or a refused string; an exception it throws reaches the caller
    /// unchanged.
    /// </summary>
    /// <remarks>
    /// A fixed clock, such as <c>() =&gt; 1700000000000</c>, makes a compact
    /// time's reading reproducible. The time comes back exact while the id
    /// and the clock fall in the same 2^40 ms window (about 34.8 years) and
    /// the id is no more than one day ahead of the clock; an id from the
    /// window before is recovered while it is less than 2^40 ms minus one day
    /// older than the clock.
    /// </remarks>
    public Func<long>? Clock { get; init; }
}
