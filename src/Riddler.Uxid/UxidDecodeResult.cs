using System.Diagnostics.CodeAnalysis;

namespace Riddler;

/// <summary>
/// The answer of <see cref="Uxid.Decode(string, UxidDecodeOptions?)"/>:
/// either the decoded value (<see cref="IsSuccess"/> true,
/// <see cref="Value"/> set) or a refusal (<see cref="IsSuccess"/> false,
/// <see cref="Error"/> and <see cref="ErrorName"/> set). Two results are
/// equal when every property is.
/// </summary>
public sealed record UxidDecodeResult
{
    private UxidDecodeResult(UxidDecoded? value, UxidDecodeError? error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>
    /// Gets whether the string decoded: true when <see cref="Value"/> is set,
    /// false when <see cref="Error"/> is.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(ErrorName))]
    public bool IsSuccess => Value is not null;

    /// <summary>Gets the decoded value, or null for a refusal.</summary>
    public UxidDecoded? Value { get; }

    /// <summary>Gets the refusal reason, or null when the string decoded.</summary>
    public UxidDecodeError? Error { get; }

    /// <summary>
    /// Gets the refusal reason's name as the wire format and its conformance
    /// corpus spell it: <c>empty</c>, <c>invalid_length</c> or
    /// <c>invalid_character</c>; null when the string decoded.
    /// </summary>
    public string? ErrorName => Error switch
    {
        null => null,
        UxidDecodeError.Empty => "empty",
        UxidDecodeError.InvalidLength => "invalid_length",
        _ => "invalid_character",
    };

    /// <summary>A result carrying a decoded value.</summary>
    internal static UxidDecodeResult Decoded(UxidDecoded value) => new(value, null);

    /// <summary>A result carrying a refusal reason.</summary>
    internal static UxidDecodeResult Refused(UxidDecodeError error) => new(null, error);
}
