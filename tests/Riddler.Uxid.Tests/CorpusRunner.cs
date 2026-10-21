using System;
using System.Linq;
using Xunit;

namespace Riddler.Tests;

// Runs one conformance case, given as its line of a case file, by the corpus
// README's rule for its kind. Each case is read strictly (see Fields): a
// member the runner does not know fails the case, and so does a missing one.
// A null prefix in a case is no prefix.
//
// The corpus does not fix the order in which a generation request's
// refusals are checked, and this port's order is not the reference's in one
// respect: it range-checks Time before the coded refusals, so a Time outside
// 0 to 2^48 - 1 with RandSize 11 throws ArgumentOutOfRangeException where the
// reference, which truncates the time, refuses with rand_size_too_large. No
// corpus case combines the two, so the runner does not rely on that order.
internal static class CorpusRunner
{
    /// <summary>
    /// The decoder clock of the decode cases, in milliseconds since the Unix
    /// epoch: inside the window the corpus README gives for its compact cases
    /// (1699913600000 ms inclusive to 2164577117183 ms exclusive).
    /// </summary>
    public const long DecoderClock = 1700000000000;

    public static void Encode(string line)
    {
        var root = Fields.Parse(line);
        _ = root.String("id");
        var input = root.Object("input");
        var options = new UxidOptions
        {
            Prefix = input.NullableString("prefix") ?? string.Empty,
            Size = Uxid.ParseSize(input.String("size")),
            CompactTime = input.Bool("compact"),
            Case = LetterCase(input.String("case")),
            Time = input.Long("time"),
            Rand = Hex.Bytes(input.String("rand")),
        };
        var expected = root.String("expected");
        input.Done();
        root.Done();

        Assert.Equal(expected, Uxid.Generate(options));
    }

    public static void Deterministic(string line)
    {
        var root = Fields.Parse(line);
        _ = root.String("id");
        var input = root.Object("input");
        var options = new UxidOptions
        {
            Prefix = input.NullableString("prefix") ?? string.Empty,
            From = input.String("from"),
            Size = Uxid.ParseSize(input.String("size")),
            Case = LetterCase(input.String("case")),
        };
        var expected = root.String("expected");
        input.Done();
        root.Done();

        Assert.Equal(expected, Uxid.Generate(options));
    }

    public static void Decode(string line)
    {
        var root = Fields.Parse(line);
        _ = root.String("id");
        var text = root.String("input");
        var expected = root.Object("expected");
        var prefix = expected.NullableString("prefix");
        var size = Uxid.ParseSize(expected.String("size"));
        var compact = expected.Bool("compact");
        var deterministic = expected.Bool("deterministic");
        var time = expected.NullableLong("time");
        expected.Done();
        root.Done();

        var result = Uxid.Decode(text, new UxidDecodeOptions { Clock = () => DecoderClock });

        Assert.True(result.IsSuccess, result.ErrorName);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorName);
        Assert.Equal(prefix, result.Value.Prefix);
        Assert.Equal(size, result.Value.Size);
        Assert.Equal(compact, result.Value.IsCompact);
        Assert.Equal(deterministic, result.Value.IsDeterministic);
        Assert.Equal(time, result.Value.Time);
    }

    public static void Invalid(string line)
    {
        var root = Fields.Parse(line);
        _ = root.String("id");
        var input = root.Object("input");
        var expectedError = root.String("expected_error");
        root.Done();

        if (input.Has("decode"))
        {
            var text = input.String("decode");
            input.Done();

            var result = Uxid.Decode(text, new UxidDecodeOptions { Clock = () => DecoderClock });

            Assert.False(result.IsSuccess);
            Assert.Null(result.Value);
            Assert.Equal(expectedError, result.ErrorName);
            Assert.Equal(DecodeError(expectedError), result.Error);
            return;
        }

        var request = input.Object("generate");
        input.Done();
        var options = new UxidOptions
        {
            Prefix = request.OptionalNullableString("prefix") ?? string.Empty,
            Size = request.OptionalString("size") is { } size ? Uxid.ParseSize(size) : UxidSize.Xl,
            CompactTime = request.OptionalBool("compact"),
            Case = request.OptionalString("case") is { } letterCase ? LetterCase(letterCase) : UxidCase.Lower,
            Time = request.OptionalLong("time"),
            Rand = request.OptionalString("rand") is { } rand ? Hex.Bytes(rand) : null,
            RandSize = request.OptionalLong("rand_size") is { } randSize ? checked((int)randSize) : null,
            From = request.OptionalString("from"),
        };
        var monotonic = request.OptionalBool("monotonic") == true;
        request.Done();

        var refusal = Record.Exception(() =>
        {
            if (monotonic)
            {
                new UxidMonotonic(options).Generate();
            }
            else
            {
                Uxid.Generate(options);
            }
        });

        Assert.Equal(expectedError, Assert.IsType<UxidException>(refusal).CodeName);
    }

    public static void Monotonic(string line)
    {
        var root = Fields.Parse(line);
        _ = root.String("id");
        var input = root.Object("input");
        var options = new UxidOptions
        {
            Prefix = input.NullableString("prefix") ?? string.Empty,
            Size = Uxid.ParseSize(input.String("size")),
            CompactTime = input.Bool("compact"),
        };
        var times = input.LongArray("times");
        var initialSeed = input.Long("initial_seed");
        var steps = input.LongArray("steps");
        var overflowSeed = input.NullableLong("overflow_seed");
        var expected = root.StringArray("expected");
        input.Done();
        root.Done();

        var seeds = overflowSeed is { } overflow ? new[] { initialSeed, overflow } : new[] { initialSeed };
        var draws = new ReplayDraws(seeds, steps);
        var generator = new UxidMonotonic(options, draws);

        Assert.Equal(expected, times.Select(generator.Generate).ToArray());
        Assert.True(draws.Exhausted, "every seed and step the case carries is drawn");
    }

    private static UxidDecodeError DecodeError(string name) => name switch
    {
        "empty" => UxidDecodeError.Empty,
        "invalid_length" => UxidDecodeError.InvalidLength,
        "invalid_character" => UxidDecodeError.InvalidCharacter,
        _ => throw new ArgumentException("not a decode refusal: " + name, nameof(name)),
    };

    private static UxidCase LetterCase(string name) => name switch
    {
        "lower" => UxidCase.Lower,
        "upper" => UxidCase.Upper,
        _ => throw new ArgumentException("not a case: " + name, nameof(name)),
    };
}
