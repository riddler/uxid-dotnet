using System;

namespace Riddler;

/// <summary>
/// The exception a refused generation request throws: an
/// <see cref="ArgumentException"/> whose <see cref="Code"/> names the
/// generation error from the closed set the wire format defines.
/// </summary>
/// <remarks>
/// Options outside what the wire format can express and that the closed set
/// does not name, such as a time before the Unix epoch or an empty
/// delimiter, throw <see cref="ArgumentException"/> or
/// <see cref="ArgumentOutOfRangeException"/> instead.
/// </remarks>
public sealed class UxidException : ArgumentException
{
    internal UxidException(UxidErrorCode code, string message)
        : base(message, "options")
    {
        Code = code;
    }

    // The three standard exception constructors are declared, as the
    // analyzers' standard-constructor rule asks, but kept private: every
    // UxidException carries a code from the closed set, so no public
    // constructor builds one without a code. Making them public later is an
    // addition; removing a public one would be a breaking change.
    private UxidException()
    {
    }

    private UxidException(string message)
        : base(message)
    {
    }

    private UxidException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Gets the generation error this refusal names.</summary>
    public UxidErrorCode Code { get; }

    /// <summary>
    /// Gets the error's name as the wire format and its conformance corpus
    /// spell it: <c>compact_time_out_of_range</c>,
    /// <c>rand_width_mismatch</c>, <c>rand_size_too_large</c>,
    /// <c>from_with_monotonic</c>, <c>rand_with_from</c> or
    /// <c>rand_with_monotonic</c>.
    /// </summary>
    public string CodeName => Code switch
    {
        UxidErrorCode.CompactTimeOutOfRange => "compact_time_out_of_range",
        UxidErrorCode.RandWidthMismatch => "rand_width_mismatch",
        UxidErrorCode.RandSizeTooLarge => "rand_size_too_large",
        UxidErrorCode.FromWithMonotonic => "from_with_monotonic",
        UxidErrorCode.RandWithFrom => "rand_with_from",
        _ => "rand_with_monotonic",
    };
}
