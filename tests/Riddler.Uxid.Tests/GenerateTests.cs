using System;
using System.Collections.Generic;
using Xunit;

namespace Riddler.Tests;

// Generation against the wire format. The conformance corpus's encode and
// generate-refusal cases run in CorpusTests; the tests here pin what the
// corpus does not fix or checks only in part.
public class GenerateTests
{
    private const long _corpusTime = 1700000000000;

    [Theory] // (sabotage: marker guard compares to 32 -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(2164663517184)]
    [InlineData(1065151889408)]
    public void RefusesCompactTimeStartingWithTheMarker(long time)
    {
        var options = new UxidOptions { Prefix = "usr", Size = UxidSize.S, CompactTime = true, Rand = Hex("010203"), Time = time };

        var refusal = Assert.Throws<UxidException>(() => Uxid.Generate(options));

        Assert.Equal(UxidErrorCode.CompactTimeOutOfRange, refusal.Code);
        Assert.Equal("compact_time_out_of_range", refusal.CodeName);
    }

    [Fact] // (sabotage: marker guard ignores CompactTime -> Assert.Null() Failure: Value is not null)
    public void StandardTimeIsNeverRefusedForTheMarker()
    {
        var options = new UxidOptions { Size = UxidSize.Xs, Time = 2164663517184 };
        string? id = null;

        var refusal = Record.Exception(() => id = Uxid.Generate(options));

        Assert.Null(refusal);
        Assert.Equal("01z0000000", id);
    }

    [Theory] // (sabotage: fixed Rand width never checked -> Assert.Throws() Failure: No exception was thrown)
    [InlineData("s", "010203")]
    [InlineData("xl", "010203040506070809")]
    [InlineData("xs", "01")]
    public void RefusesFixedRandOfAnotherWidth(string size, string rand)
    {
        var options = new UxidOptions { Prefix = "usr", Size = Uxid.ParseSize(size), Rand = Hex(rand), Time = _corpusTime };

        var refusal = Assert.Throws<UxidException>(() => Uxid.Generate(options));

        Assert.Equal(UxidErrorCode.RandWidthMismatch, refusal.Code);
        Assert.Equal("rand_width_mismatch", refusal.CodeName);
    }

    [Fact] // (sabotage: RandSize cap raised to 11 -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesRandSizeAboveTenBytes()
    {
        var options = new UxidOptions { Prefix = "usr", Size = UxidSize.Xl, RandSize = 11, Time = _corpusTime };

        var refusal = Assert.Throws<UxidException>(() => Uxid.Generate(options));

        Assert.Equal(UxidErrorCode.RandSizeTooLarge, refusal.Code);
        Assert.Equal("rand_size_too_large", refusal.CodeName);
        Assert.IsAssignableFrom<ArgumentException>(refusal);
    }

    [Fact] // (sabotage: RandSize ignored -> Assert.Equal() Failure: Strings differ)
    public void RandSizeReplacesTheSizeWidth()
    {
        var options = new UxidOptions { Prefix = "usr", Size = UxidSize.Xl, RandSize = 2, Time = _corpusTime, ByteSource = AllOnes };

        Assert.Equal("usr_01hf7yat00zzz1", Uxid.Generate(options));
    }

    [Fact] // (sabotage: check lets -1 through -> Assert.Throws() Failure: Exception type was not an exact match)
    public void RefusesNegativeRandSizeOutsideTheClosedSet()
    {
        var options = new UxidOptions { RandSize = -1, Time = _corpusTime };

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Fact] // (sabotage: ByteSource ignored -> Assert.Equal() Failure: Strings differ)
    public void AsksTheByteSourceForTheFieldWidth()
    {
        var asked = new List<int>();
        var options = new UxidOptions
        {
            Prefix = "cus",
            Size = UxidSize.M,
            CompactTime = true,
            Time = _corpusTime,
            ByteSource = count =>
            {
                asked.Add(count);
                return Hex("ffffffffffff");
            },
        };

        Assert.Equal("cus_hf7yat00zzzzzzzzz7", Uxid.Generate(options));
        Assert.Equal(6, Assert.Single(asked));
    }

    [Fact] // (sabotage: empty field calls the source -> Assert.Equal() Failure: Values differ)
    public void NeverCallsTheByteSourceForAnEmptyField()
    {
        var calls = 0;
        var options = new UxidOptions
        {
            Size = UxidSize.Xs,
            Time = _corpusTime,
            ByteSource = count =>
            {
                calls++;
                return new byte[count];
            },
        };

        Assert.Equal("01hf7yat00", Uxid.Generate(options));
        Assert.Equal(0, calls);
    }

    [Theory] // (sabotage: length check never fires -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(1)]
    [InlineData(3)]
    public void RefusesAByteSourceReturningAnotherLength(int length)
    {
        var options = new UxidOptions { Size = UxidSize.S, Time = _corpusTime, ByteSource = _ => new byte[length] };

        Assert.Throws<InvalidOperationException>(() => Uxid.Generate(options));
    }

    [Fact] // (sabotage: null result unchecked -> Assert.Throws() Failure: Exception type was not an exact match)
    public void RefusesAByteSourceReturningNull()
    {
        var options = new UxidOptions { Size = UxidSize.S, Time = _corpusTime, ByteSource = _ => null! };

        Assert.Throws<InvalidOperationException>(() => Uxid.Generate(options));
    }

    [Fact] // (sabotage: default source leaves zeros -> Assert.NotEqual() Failure: Strings are equal)
    public void DefaultsToAnUnprefixedExtraLargeStandardLowerCaseIdWithRandomBytes()
    {
        var first = Uxid.Generate();
        var second = Uxid.Generate(new UxidOptions());

        Assert.Equal(26, first.Length);
        Assert.Equal(26, second.Length);
        Assert.InRange(first[0], '0', '7');
        Assert.DoesNotMatch("[A-Z]", first);
        Assert.NotEqual(first.Substring(10), second.Substring(10));
    }

    [Fact] // (sabotage: default time 0 -> Assert.InRange() Failure: Value not in range)
    public void DefaultTimeIsTheSystemClockAtTheCall()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = Uxid.Generate(new UxidOptions { Size = UxidSize.Xs });
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.InRange(ReadStandardTime(id), before, after);
    }

    [Theory] // (sabotage: range widened by one each end -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(-1)]
    [InlineData(281474976710656)]
    public void RefusesATimeOutsideTheStandardField(long time)
    {
        var options = new UxidOptions { Size = UxidSize.Xs, Time = time };

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Theory] // (sabotage: only null refused -> Assert.Throws() Failure: No exception was thrown)
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnEmptyDelimiter(string? delimiter)
    {
        var options = new UxidOptions { Prefix = "usr", Delimiter = delimiter!, Time = _corpusTime };

        var refusal = Assert.Throws<ArgumentException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Fact] // (sabotage: delimiter fixed at _ -> Assert.Equal() Failure: Strings differ)
    public void JoinsPrefixAndBodyWithTheDelimiter()
    {
        var options = new UxidOptions { Prefix = "usr", Delimiter = "--", Size = UxidSize.Xs, Time = _corpusTime };

        Assert.Equal("usr--01hf7yat00", Uxid.Generate(options));
    }

    [Fact] // (sabotage: null prefix read as none -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesANullPrefix()
    {
        var options = new UxidOptions { Prefix = null!, Time = _corpusTime };

        var refusal = Assert.Throws<ArgumentException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Fact] // (sabotage: case applied to the prefix -> Assert.Equal() Failure: Strings differ)
    public void KeepsThePrefixCaseInAnUpperCaseId()
    {
        var options = new UxidOptions { Prefix = "Usr", Case = UxidCase.Upper, Size = UxidSize.S, Rand = Hex("ffff"), Time = _corpusTime };

        Assert.Equal("Usr_01HF7YAT00ZZZ1", Uxid.Generate(options));
    }

    [Theory] // (sabotage: size check, then case check, removed -> Assert.Throws() Failure: No exception was thrown)
    [InlineData((UxidSize)5, UxidCase.Lower)]
    [InlineData(UxidSize.Xl, (UxidCase)2)]
    public void RefusesASizeOrCaseOutsideItsEnum(UxidSize size, UxidCase letterCase)
    {
        var options = new UxidOptions { Size = size, Case = letterCase, Time = _corpusTime };

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Theory] // (sabotage: medium parses as L -> Assert.Equal() Failure: Values differ)
    [InlineData("xs", UxidSize.Xs)]
    [InlineData("xsmall", UxidSize.Xs)]
    [InlineData("s", UxidSize.S)]
    [InlineData("small", UxidSize.S)]
    [InlineData("m", UxidSize.M)]
    [InlineData("medium", UxidSize.M)]
    [InlineData("l", UxidSize.L)]
    [InlineData("large", UxidSize.L)]
    [InlineData("xl", UxidSize.Xl)]
    [InlineData("xlarge", UxidSize.Xl)]
    public void ParsesShortAndLongSizeSpellings(string text, UxidSize expected)
    {
        Assert.Equal(expected, Uxid.ParseSize(text));
    }

    [Theory] // (sabotage: unknown spelling reads as Xl -> Assert.Throws() Failure: No exception was thrown)
    [InlineData("XL")]
    [InlineData("huge")]
    [InlineData("")]
    public void RefusesAnythingElseAsASize(string text)
    {
        var refusal = Assert.Throws<ArgumentException>(() => Uxid.ParseSize(text));

        Assert.Equal("text", refusal.ParamName);
    }

    [Fact] // (sabotage: null arm removed -> Assert.Throws() Failure: Exception type was not an exact match)
    public void RefusesANullSizeSpelling()
    {
        Assert.Throws<ArgumentNullException>(() => Uxid.ParseSize(null!));
    }

    [Theory] // (sabotage: rand_with_from misspelt -> Assert.Equal() Failure: Strings differ)
    [InlineData(UxidErrorCode.CompactTimeOutOfRange, "compact_time_out_of_range")]
    [InlineData(UxidErrorCode.RandWidthMismatch, "rand_width_mismatch")]
    [InlineData(UxidErrorCode.RandSizeTooLarge, "rand_size_too_large")]
    [InlineData(UxidErrorCode.FromWithMonotonic, "from_with_monotonic")]
    [InlineData(UxidErrorCode.RandWithFrom, "rand_with_from")]
    [InlineData(UxidErrorCode.RandWithMonotonic, "rand_with_monotonic")]
    public void NamesEveryErrorCodeAsTheCorpusDoes(UxidErrorCode code, string expected)
    {
        Assert.Equal(expected, CodeNameOf(code));
    }

    // The error names are read off an exception, the one place they appear.
    // Two of the codes name refusals of monotonic generation, which no
    // public call in the library makes, so the exception is built by
    // reflection over its internal constructor.
    private static string CodeNameOf(UxidErrorCode code)
    {
        var exception = (UxidException)Activator.CreateInstance(
            typeof(UxidException),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [code, "message"],
            culture: null)!;
        return exception.CodeName;
    }

    private static byte[] AllOnes(int count)
    {
        var bytes = new byte[count];
        for (var index = 0; index < count; index++)
        {
            bytes[index] = 0xff;
        }

        return bytes;
    }

    private static byte[] Hex(string? text)
    {
        text ??= string.Empty;
        var bytes = new byte[text.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(text.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

    private static long ReadStandardTime(string body)
    {
        const string alphabet = "0123456789abcdefghjkmnpqrstvwxyz";
        long time = 0;
        for (var index = 0; index < 10; index++)
        {
            time = (time << 5) | (long)Array.IndexOf(alphabet.ToCharArray(), body[index]);
        }

        return time;
    }
}
