using System;
using Xunit;

namespace Riddler.Tests;

// Decoding against the wire format. The vectors are the conformance corpus's
// decode cases and its decode refusals (uxid-ex at v2.10.0,
// conformance/decode.json and conformance/invalid.json), copied as their
// inputs and expected answers; the full corpus runner replaces them as the
// arbiter once the corpus is vendored. The clock window is the corpus
// README's: its compact cases hold for a decoder clock from one day before
// 1700000000000 ms, inclusive, to one day before 2^40 + 1065151889407 ms,
// exclusive.
public class DecodeTests
{
    private const long _corpusTime = 1700000000000;
    private const long _oneDay = 86400000;
    private const long _window = 1L << 40;
    private const long _guardLastValid = 1065151889407;
    private const long _windowStart = _corpusTime - _oneDay;
    private const long _windowEnd = _window + _guardLastValid - _oneDay;

    [Theory] // (sabotage: standard time keeps all 5 bits of its first symbol -> Assert.Equal() Failure: Values differ)
    [InlineData("usr_01hf7yat0004hmasw9nf6yy093", "usr", "xl", false, false, 1700000000000L)]
    [InlineData("usr_hf7yat0004hmasw9nf6yy093", "usr", "xl", true, false, 1700000000000L)]
    [InlineData("usr_01hf7yat0004hmasw9nf61", "usr", "l", false, false, 1700000000000L)]
    [InlineData("usr_hf7yat0004hmasw9nf6yf", "usr", "l", true, false, 1700000000000L)]
    [InlineData("usr_01hf7yat0004hmasw9", "usr", "m", false, false, 1700000000000L)]
    [InlineData("usr_hf7yat0004hmasw9n3", "usr", "m", false, false, 51950139736068L)]
    [InlineData("usr_01hf7yat0004h1", "usr", "s", false, false, 1700000000000L)]
    [InlineData("usr_hf7yat0004hm5", "usr", "s", true, false, 1700000000000L)]
    [InlineData("usr_01hf7yat00", "usr", "xs", false, false, 1700000000000L)]
    [InlineData("usr_hf7yat0001", "usr", "xs", false, false, 51950139736065L)]
    [InlineData("usr_01HF7YAT0004HMASW9NF6YY093", "usr", "xl", false, false, 1700000000000L)]
    [InlineData("01hf7yat0004hmasw9nf6yy093", null, "xl", false, false, 1700000000000L)]
    [InlineData("a_b_01hf7yat000001", "a_b", "s", false, false, 1700000000000L)]
    [InlineData("usr_yzzzzzzz04hm5", "usr", "s", true, false, 1065151889407L)]
    [InlineData("usr_zcvt7epac0t1ebcsjfyf7cwz25", "usr", "xl", false, true, null)]
    [InlineData("cus_zt9vnyfd85bff0", "cus", "s", false, true, null)]
    [InlineData("zqz4cwzgyw8ks97869", null, "m", false, true, null)]
    [InlineData("usr_ZCVT7EPAC0T1EBCSJFYF7CWZ25", "usr", "xl", false, true, null)]
    public void DecodesCorpusCases(string input, string? prefix, string size, bool compact, bool deterministic, long? time)
    {
        var result = Uxid.Decode(input, AtClock(_corpusTime));

        Assert.True(result.IsSuccess);
        AssertDecoded(result.Value, prefix, Uxid.ParseSize(size), compact, deterministic, time);
        Assert.Null(result.Error);
        Assert.Null(result.ErrorName);
    }

    [Theory] // (sabotage: alphabet checked before length -> Assert.Equal() Failure: Values differ)
    [InlineData("", UxidDecodeError.Empty, "empty")]
    [InlineData("usr_", UxidDecodeError.Empty, "empty")]
    [InlineData("a_b_", UxidDecodeError.Empty, "empty")]
    [InlineData("_", UxidDecodeError.Empty, "empty")]
    [InlineData("usr_01hf7yat000", UxidDecodeError.InvalidLength, "invalid_length")]
    [InlineData("usr_01hf7yat00000g40r40m30e20", UxidDecodeError.InvalidLength, "invalid_length")]
    [InlineData("usr_01hf7yat00000g40r40m30e2090", UxidDecodeError.InvalidLength, "invalid_length")]
    [InlineData("usr", UxidDecodeError.InvalidLength, "invalid_length")]
    [InlineData("usr_01hu7yat00000g40r40m30e209", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("usr_01hf7yat00000g4lr40m30e209", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("usr_01hf7yat00000g40r40mO0e209", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("usr_01hf7yat00-00g40r40m30e209", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("usr_zcvt7epac0t1ebcsjfyf7cwzi5", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("01hf7yat00000g40r40m30e2é", UxidDecodeError.InvalidCharacter, "invalid_character")]
    [InlineData("01hf7yat00000g40r40m30e20é", UxidDecodeError.InvalidLength, "invalid_length")]
    [InlineData("usr_01hf7yat00u", UxidDecodeError.InvalidLength, "invalid_length")]
    public void RefusesCorpusDecodeCases(string input, UxidDecodeError error, string name)
    {
        var result = Uxid.Decode(input, AtClock(_corpusTime));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Equal(error, result.Error);
        Assert.Equal(name, result.ErrorName);
    }

    [Theory] // (sabotage: o read as a symbol (0) -> Assert.Equal() Failure: Values differ)
    [InlineData('I')]
    [InlineData('i')]
    [InlineData('L')]
    [InlineData('l')]
    [InlineData('O')]
    [InlineData('o')]
    [InlineData('U')]
    [InlineData('u')]
    [InlineData('*')]
    [InlineData(' ')]
    public void RefusesEveryCharacterOutsideTheAlphabet(char outside)
    {
        var result = Uxid.Decode("usr_01hf7yat00" + outside + "001");

        Assert.Equal(UxidDecodeError.InvalidCharacter, result.Error);
    }

    [Fact] // (sabotage: Value maps j to 19 -> Assert.Equal() Failure: Values differ)
    public void ReadsEverySymbolInBothCases()
    {
        const string lower = "0123456789abcdefghjkmnpqrstvwxyz";
        for (var value = 0; value < 32; value++)
        {
            // A standard xs body: the time's last symbol carries the value.
            foreach (var symbol in new[] { lower[value], char.ToUpperInvariant(lower[value]) })
            {
                var result = Uxid.Decode("000000000" + symbol);

                Assert.Equal(value, result.Value?.Time);
            }
        }
    }

    [Fact] // (sabotage: compact-time step-back uses >= one day -> Assert.Equal() Failure: Values differ)
    public void RecoversCompactCasesFromTheFirstClockOfTheWindow()
    {
        Assert.Equal(1699913600000, _windowStart);

        AssertCompactCasesRecovered(_windowStart);
        Assert.Equal(_corpusTime - _window, CompactTime("usr_hf7yat0004hm5", _windowStart - 1));
    }

    [Fact] // (sabotage: compact-time step-back removed -> Assert.Equal() Failure: Values differ)
    public void RecoversCompactCasesUntilTheLastClockOfTheWindow()
    {
        Assert.Equal(2164577117183, _windowEnd);

        AssertCompactCasesRecovered(_windowEnd - 1);
        Assert.Equal(_window + _guardLastValid, CompactTime("usr_yzzzzzzz04hm5", _windowEnd));
    }

    [Theory] // (sabotage: step-back only when the result is at least 2^40 -> Assert.Equal() Failure: Values differ)
    [InlineData("usr_hf7yat0004hm5", 0L, -499023255552L)]
    [InlineData("usr_yzzzzzzz04hm5", 0L, -34359738369L)]
    [InlineData("usr_hf7yat0004hm5", 600488372224L, 600488372224L)]
    [InlineData("usr_00000001zzzzz", 0L, 1L)]
    public void StepsBackBelowZeroFromTheFirstWindow(string id, long clock, long expected)
    {
        Assert.Equal(expected, CompactTime(id, clock));
    }

    [Fact] // (sabotage: system clock read as 0 when no clock is set -> Assert.Equal() Failure: Values differ)
    public void ReadsTheSystemClockWhenNoClockIsSet()
    {
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var id = Uxid.Generate(new UxidOptions { Prefix = "usr", Size = UxidSize.S, CompactTime = true, Time = before });

        Assert.Equal(before, Uxid.Decode(id).Value?.Time);
        Assert.True(Uxid.TryDecode(id, out var decoded));
        Assert.Equal(before, decoded.Time);
    }

    [Fact] // (sabotage: clock read before the body is checked -> Assert.Null() Failure: Value is not null)
    public void ReadsTheClockOnlyForACompactTimeBody()
    {
        var failing = new UxidDecodeOptions { Clock = () => throw new InvalidOperationException("The clock was read.") };

        var refusal = Record.Exception(() =>
        {
            Assert.True(Uxid.Decode("usr_01hf7yat0004h1", failing).IsSuccess);
            Assert.True(Uxid.Decode("usr_zt9vnyfd85bff", failing).IsSuccess);
            Assert.False(Uxid.Decode("usr_", failing).IsSuccess);
            Assert.False(Uxid.Decode("usr_hf7yat0004hm", failing).IsSuccess);
        });

        Assert.Null(refusal);
        Assert.Throws<InvalidOperationException>(() => Uxid.Decode("usr_hf7yat0004hm5", failing));
    }

    [Fact] // (sabotage: compact flag cleared for a marker body -> Assert.Equal() Failure: Values differ)
    public void ReadsACompactLengthMarkerBodyAsDeterministicAndCompact()
    {
        var result = Uxid.Decode("usr_zt9vnyfd85bff");

        AssertDecoded(result.Value, "usr", UxidSize.S, true, true, null);
    }

    [Theory] // (sabotage: split at the last delimiter occurrence, overlaps allowed -> Assert.Equal() Failure: Values differ)
    [InlineData("cus--01hf7yat0004h1", "--", "cus", null)]
    [InlineData("a--b--01hf7yat0004h1", "--", "a--b", null)]
    [InlineData("usr---01hf7yat0004h1", "--", null, UxidDecodeError.InvalidLength)]
    [InlineData("_01hf7yat0004h1", "_", "", null)]
    [InlineData("01hf7yat0004h1", "--", null, null)]
    public void SplitsAtTheLastNonOverlappingDelimiter(string id, string delimiter, string? prefix, UxidDecodeError? error)
    {
        var result = Uxid.Decode(id, new UxidDecodeOptions { Delimiter = delimiter });

        Assert.Equal(error, result.Error);
        Assert.Equal(prefix, result.Value?.Prefix);
    }

    [Fact] // (sabotage: a null id answered as empty -> Assert.Throws() Failure: No exception was thrown)
    public void ThrowsOnlyForANullIdOrAMissingDelimiter()
    {
        Assert.Equal("id", Assert.Throws<ArgumentNullException>(() => Uxid.Decode(null!)).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() => Uxid.Decode("usr_01hf7yat00", new UxidDecodeOptions { Delimiter = "" })).ParamName);
        Assert.Equal("options", Assert.Throws<ArgumentException>(() => Uxid.Decode("usr_01hf7yat00", new UxidDecodeOptions { Delimiter = null! })).ParamName);
    }

    [Theory] // (sabotage: TryDecode answers true for a refusal -> Assert.Equal() Failure: Values differ)
    [InlineData("usr_01hf7yat0004h1", true)]
    [InlineData("usr_01hf7yat0004h12", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TryDecodeAnswersWhetherTheIdDecoded(string? id, bool expected)
    {
        var answer = Uxid.TryDecode(id, out var decoded);

        Assert.Equal(expected, answer);
        Assert.Equal(expected, decoded is not null);
        if (decoded is not null)
        {
            Assert.Equal(1700000000000, decoded.Time);
        }
    }

    [Theory] // (sabotage: length 21 read as standard l -> Assert.Equal() Failure: Values differ)
    [InlineData(UxidSize.Xs, false)]
    [InlineData(UxidSize.S, false)]
    [InlineData(UxidSize.S, true)]
    [InlineData(UxidSize.M, false)]
    [InlineData(UxidSize.L, false)]
    [InlineData(UxidSize.L, true)]
    [InlineData(UxidSize.Xl, false)]
    [InlineData(UxidSize.Xl, true)]
    public void DecodesWhatGenerateBuilds(UxidSize size, bool compact)
    {
        var id = Uxid.Generate(new UxidOptions { Prefix = "cus", Size = size, CompactTime = compact, Time = _corpusTime });

        AssertDecoded(Uxid.Decode(id, AtClock(_corpusTime)).Value, "cus", size, compact, false, _corpusTime);
    }

    [Fact] // (sabotage: UxidDecoded and UxidDecodeResult declared as classes -> Assert.Equal() Failure: Values differ)
    public void ResultsAndDecodedValuesCompareByValue()
    {
        Assert.Equal(Uxid.Decode("usr_01hf7yat0004h1"), Uxid.Decode("usr_01hf7yat0004h1"));
        Assert.Equal(Uxid.Decode("usr_"), Uxid.Decode("cus_"));
        Assert.NotEqual(Uxid.Decode("usr_01hf7yat0004h1").Value, Uxid.Decode("cus_01hf7yat0004h1").Value);
    }

    private static UxidDecodeOptions AtClock(long clock) => new() { Clock = () => clock };

    private static long? CompactTime(string id, long clock) => Uxid.Decode(id, AtClock(clock)).Value?.Time;

    private static void AssertDecoded(UxidDecoded? actual, string? prefix, UxidSize size, bool compact, bool deterministic, long? time)
    {
        Assert.NotNull(actual);
        Assert.Equal(prefix, actual.Prefix);
        Assert.Equal(size, actual.Size);
        Assert.Equal(compact, actual.IsCompact);
        Assert.Equal(deterministic, actual.IsDeterministic);
        Assert.Equal(time, actual.Time);
    }

    private static void AssertCompactCasesRecovered(long clock)
    {
        Assert.Equal(_corpusTime, CompactTime("usr_hf7yat0004hmasw9nf6yy093", clock));
        Assert.Equal(_corpusTime, CompactTime("usr_hf7yat0004hmasw9nf6yf", clock));
        Assert.Equal(_corpusTime, CompactTime("usr_hf7yat0004hm5", clock));
        Assert.Equal(_guardLastValid, CompactTime("usr_yzzzzzzz04hm5", clock));
    }
}
