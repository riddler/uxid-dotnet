using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Riddler.Tests;

// Monotonic generation against the wire format's rule. The first theory's
// rows are the conformance corpus's monotonic cases (uxid-ex at v2.10.0,
// conformance/monotonic.json), copied as their inputs, draws and expected
// ids, and the refusals are its two monotonic generate-refusal cases
// (conformance/invalid.json); the full corpus runner replaces them as the
// arbiter once the corpus is vendored.
public class MonotonicTests
{
    private const long _corpusTime = 1700000000000;

    // 31 x 2^35: the first compact time whose first character is the marker.
    private const long _markerTime = 1065151889408;

    private const long _maxTime = (1L << 48) - 1;

    private static readonly int[] _fiveThenThree = [5, 3];

    private static readonly int[] _twoThenOne = [2, 1];

    [Theory] // (sabotage: overflow keeps T, not T + 1 -> Assert.Equal() Failure: Collections differ)
    [InlineData("usr", "xl", false, new[] { 1700000000000, 1700000000000, 1700000000000, 1700000000000 }, 123456789L, new[] { 1L, 1099511627776, 42 }, null, new[] { "usr_01hf7yat0000000000003nqk8n", "usr_01hf7yat0000000000003nqk8p", "usr_01hf7yat0000000001003nqk8p", "usr_01hf7yat0000000001003nqka0" })]
    [InlineData("usr", "m", false, new[] { 1700000000000, 1699999999999, 1699999999990, 1700000000000 }, 1000L, new[] { 5L, 1048576, 7 }, null, new[] { "usr_01hf7yat00000000z8", "usr_01hf7yat00000000zd", "usr_01hf7yat00000100zd", "usr_01hf7yat00000100zm" })]
    [InlineData("usr", "s", false, new[] { 1700000000000, 1700000000000, 1700000000000, 1700000000000, 1700000000000, 1700000000001 }, 65000L, new[] { 200L, 250, 100, 7, 3 }, 1234L, new[] { "usr_01hf7yat00zqm0", "usr_01hf7yat00ztr0", "usr_01hf7yat00zyn0", "usr_01hf7yat010k90", "usr_01hf7yat010kc1", "usr_01hf7yat010ke0" })]
    [InlineData("usr", "xs", true, new[] { 1700000000000, 1700000000000, 1699999999999 }, 250L, new[] { 5L, 16 }, 9L, new[] { "usr_hf7yat00z2", "usr_hf7yat00z7", "usr_hf7yat0111" })]
    [InlineData("usr", "l", true, new[] { 1700000000000 }, 0L, new long[0], null, new[] { "usr_hf7yat000000000000000" })]
    [InlineData(null, "s", true, new[] { 1700000000000, 1700000000000 }, 0L, new[] { 4096L }, null, new[] { "hf7yat0000000", "hf7yat0000800" })]
    public void ReplaysCorpusCases(string? prefix, string size, bool compact, long[] times, long initialSeed, long[] steps, long? overflowSeed, string[] expected)
    {
        ArgumentNullException.ThrowIfNull(times);
        ArgumentNullException.ThrowIfNull(steps);
        var seeds = new List<long> { initialSeed };
        if (overflowSeed is long seed)
        {
            seeds.Add(seed);
        }

        var draws = new ReplayDraws(seeds, steps);
        var generator = new UxidMonotonic(
            new UxidOptions { Prefix = prefix ?? string.Empty, Size = Uxid.ParseSize(size), CompactTime = compact },
            draws);

        var ids = times.Select(generator.Generate).ToArray();

        Assert.Equal(expected, ids);
        Assert.True(draws.Exhausted, "every seed and step the case carries is drawn");
    }

    [Fact] // (sabotage: From check removed -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesFromWithMonotonic()
    {
        var options = new UxidOptions { Prefix = "usr", From = "alice@example.com" };

        var refusal = Assert.Throws<UxidException>(() => new UxidMonotonic(options));

        Assert.Equal(UxidErrorCode.FromWithMonotonic, refusal.Code);
        Assert.Equal("from_with_monotonic", refusal.CodeName);
    }

    [Fact] // (sabotage: Rand check removed -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesRandWithMonotonic()
    {
        var options = new UxidOptions { Prefix = "usr", Size = UxidSize.S, Rand = Hex("0102"), Time = _corpusTime };

        var refusal = Assert.Throws<UxidException>(() => new UxidMonotonic(options));

        Assert.Equal(UxidErrorCode.RandWithMonotonic, refusal.Code);
        Assert.Equal("rand_with_monotonic", refusal.CodeName);
    }

    [Fact] // (sabotage: From check skipped when Rand is set -> Assert.Equal() Failure: Values differ)
    public void ChecksFromBeforeRand()
    {
        var options = new UxidOptions { From = "alice@example.com", Rand = Hex("0102") };

        var refusal = Assert.Throws<UxidException>(() => new UxidMonotonic(options));

        Assert.Equal(UxidErrorCode.FromWithMonotonic, refusal.Code);
    }

    [Fact] // (sabotage: step mask 4w - 1 bits -> Assert.Equal() Failure: Strings differ)
    public void DrawsTheStepAsTheLowHalfOfTheFieldsBitsPlusOne()
    {
        var asked = new List<int>();
        var generator = new UxidMonotonic(new UxidOptions
        {
            Prefix = "usr",
            Size = UxidSize.M,
            Time = _corpusTime,
            ByteSource = count =>
            {
                asked.Add(count);
                return asked.Count == 1 ? new byte[count] : Enumerable.Repeat((byte)0xff, count).ToArray();
            },
        });

        generator.Generate();
        var second = generator.Generate();

        // Five bytes of field: a step of 3 bytes, all ones, masked to 20 bits, plus one, is 2^20.
        Assert.Equal(Expected("usr", UxidSize.M, false, _corpusTime, "0000100000"), second);
        Assert.Equal(_fiveThenThree, asked);
    }

    [Fact] // (sabotage: step drawn from w bytes, not ceil(w / 2) -> Assert.Equal() Failure: Collections differ)
    public void DrawsOneByteForTheStepOfATwoByteField()
    {
        var asked = new List<int>();
        var generator = new UxidMonotonic(new UxidOptions
        {
            Prefix = "usr",
            Size = UxidSize.S,
            ByteSource = count =>
            {
                asked.Add(count);
                return asked.Count == 1 ? Hex("0001") : Enumerable.Repeat((byte)0xff, count).ToArray();
            },
        });

        generator.Generate(_corpusTime);
        var second = generator.Generate(_corpusTime);

        Assert.Equal(Expected("usr", UxidSize.S, false, _corpusTime, "0101"), second);
        Assert.Equal(_twoThenOne, asked);
    }

    [Fact] // (sabotage: CompactTime default false at Xs -> Assert.Null() Failure: Value is not null)
    public void UsesCompactTimeAtXsByDefault()
    {
        UxidMonotonic? generator = null;

        var refusal = Record.Exception(() => generator = new UxidMonotonic(new UxidOptions { Prefix = "usr", Size = UxidSize.Xs, ByteSource = _ => Hex("ab") }));

        Assert.Null(refusal);
        Assert.Equal(Expected("usr", UxidSize.Xs, true, _corpusTime, "ab"), generator!.Generate(_corpusTime));
    }

    [Theory] // (sabotage: empty field not refused -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(UxidSize.Xs, false, null)]
    [InlineData(UxidSize.Xl, null, 0)]
    public void RefusesAnEmptyRandomField(UxidSize size, bool? compact, int? randSize)
    {
        var options = new UxidOptions { Size = size, CompactTime = compact, RandSize = randSize };

        var refusal = Assert.Throws<ArgumentException>(() => new UxidMonotonic(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Fact] // (sabotage: RandSize width ignored -> Assert.Equal() Failure: Strings differ)
    public void TakesAnExplicitRandomFieldWidth()
    {
        var generator = new UxidMonotonic(
            new UxidOptions { Prefix = "usr", Size = UxidSize.M, RandSize = 2 },
            new ReplayDraws([0x0102], []));

        Assert.Equal(Expected("usr", UxidSize.M, false, _corpusTime, "0102", 2), generator.Generate(_corpusTime));
    }

    [Fact] // (sabotage: RandSize above 10 accepted -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesARandomFieldWiderThanTenBytes()
    {
        var refusal = Assert.Throws<UxidException>(() => new UxidMonotonic(new UxidOptions { RandSize = 11 }));

        Assert.Equal(UxidErrorCode.RandSizeTooLarge, refusal.Code);
    }

    [Theory] // (sabotage: RandSize, Time and size range checks removed -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(-1, null, null)]
    [InlineData(null, -1L, null)]
    [InlineData(null, 281474976710656L, null)]
    [InlineData(null, null, 9)]
    public void RefusesOptionsOutOfRange(int? randSize, long? time, int? size)
    {
        var options = new UxidOptions { RandSize = randSize, Time = time, Size = (UxidSize?)size ?? UxidSize.Xl };

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => new UxidMonotonic(options));

        Assert.Equal("options", refusal.ParamName);
    }

    [Fact] // (sabotage: null prefix taken as none -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesANullPrefixAndAnEmptyDelimiter()
    {
        Assert.Throws<ArgumentException>(() => new UxidMonotonic(new UxidOptions { Prefix = null! }));
        Assert.Throws<ArgumentException>(() => new UxidMonotonic(new UxidOptions { Delimiter = string.Empty }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UxidMonotonic(new UxidOptions { Case = (UxidCase)7 }));
    }

    [Fact] // (sabotage: case not applied -> Assert.Equal() Failure: Strings differ)
    public void AppliesTheCaseAndTheDelimiter()
    {
        var generator = new UxidMonotonic(
            new UxidOptions { Prefix = "cus", Size = UxidSize.S, Case = UxidCase.Upper, Delimiter = "-" },
            new ReplayDraws([0xffff], []));

        Assert.Equal("cus-01HF7YAT00ZZZ1", generator.Generate(_corpusTime));
    }

    [Fact] // (sabotage: Generate() ignores the options' Time -> Assert.Equal() Failure: Strings differ)
    public void GeneratesAtTheOptionsTimeWhenSet()
    {
        var generator = new UxidMonotonic(
            new UxidOptions { Prefix = "usr", Size = UxidSize.S, Time = _corpusTime },
            new ReplayDraws([0x0123], [1]));

        Assert.Equal("usr_01hf7yat0004h1", generator.Generate());
        Assert.Equal(Expected("usr", UxidSize.S, false, _corpusTime, "0124"), generator.Generate());
    }

    [Fact] // (sabotage: Generate() reads a fixed time when none is set -> Assert.InRange() Failure: Value not in range)
    public void GeneratesAtTheSystemClockWhenNoTimeIsSet()
    {
        var generator = new UxidMonotonic(new UxidOptions { Size = UxidSize.S });
        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var id = generator.Generate();

        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Assert.InRange(Assert.IsType<long>(Uxid.Decode(id).Value!.Time), before, after);
    }

    [Theory] // (sabotage: time range check removed -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(-1L)]
    [InlineData(281474976710656L)]
    public void RefusesATimeOutsideTheFieldAndKeepsItsState(long time)
    {
        var draws = new ReplayDraws([0x0100], [3]);
        var generator = new UxidMonotonic(new UxidOptions { Prefix = "usr", Size = UxidSize.S }, draws);
        generator.Generate(_corpusTime);

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(time));

        Assert.Equal("time", refusal.ParamName);
        Assert.Equal(Expected("usr", UxidSize.S, false, _corpusTime, "0103"), generator.Generate(_corpusTime));
        Assert.True(draws.Exhausted);
    }

    [Fact] // (sabotage: state written before the marker check -> Assert.Null() Failure: Value is not null)
    public void KeepsItsStateAfterAnOverflowOntoARefusedCompactTime()
    {
        // The last id is at the last compact time before the marker, with the
        // field at 250. A step of 16 overflows onto the marker's time and is
        // refused; the next call steps from 250 again, and a step of 5 fits.
        var draws = new ReplayDraws([250, 9], [16, 5]);
        var generator = new UxidMonotonic(new UxidOptions { Prefix = "usr", Size = UxidSize.Xs }, draws);
        generator.Generate(_markerTime - 1);

        var refusal = Assert.Throws<UxidException>(() => generator.Generate(_markerTime - 1));

        Assert.Equal(UxidErrorCode.CompactTimeOutOfRange, refusal.Code);
        string? next = null;
        Assert.Null(Record.Exception(() => next = generator.Generate(_markerTime - 1)));
        Assert.Equal(Expected("usr", UxidSize.Xs, true, _markerTime - 1, "ff"), next);
        Assert.True(draws.Exhausted);
    }

    [Fact] // (sabotage: overflow at the last millisecond moves to 2^48 -> Assert.Throws() Failure: Exception type was not an exact match)
    public void RefusesAnOverflowInTheLastMillisecondAndKeepsItsState()
    {
        var draws = new ReplayDraws([250], [16, 5]);
        var generator = new UxidMonotonic(new UxidOptions { Size = UxidSize.Xs, CompactTime = false, RandSize = 1 }, draws);
        generator.Generate(_maxTime);

        var refusal = Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(_maxTime));

        Assert.Equal("time", refusal.ParamName);
        Assert.Equal(Expected(string.Empty, UxidSize.Xs, false, _maxTime, "ff", 1), generator.Generate(_maxTime));
        Assert.True(draws.Exhausted);
    }

    [Fact] // (sabotage: seed length never checked -> Assert.Throws() Failure: No exception was thrown)
    public void RefusesASeedOfTheWrongWidth()
    {
        var generator = new UxidMonotonic(new UxidOptions { Size = UxidSize.S }, new FixedDraws(Hex("010203"), 1));

        Assert.Throws<InvalidOperationException>(() => generator.Generate(_corpusTime));
    }

    [Theory] // (sabotage: step range never checked -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(0L)]
    [InlineData(257L)]
    public void RefusesAStepOutsideItsRange(long step)
    {
        var generator = new UxidMonotonic(new UxidOptions { Size = UxidSize.S }, new FixedDraws(Hex("0000"), step));
        generator.Generate(_corpusTime);

        Assert.Throws<InvalidOperationException>(() => generator.Generate(_corpusTime));
    }

    [Fact] // (sabotage: byte source used for the seed despite draws -> Assert.Equal() Failure: Strings differ)
    public void NeverCallsTheByteSourceWhenGivenDraws()
    {
        var calls = 0;
        var generator = new UxidMonotonic(
            new UxidOptions
            {
                Size = UxidSize.S,
                ByteSource = count =>
                {
                    calls++;
                    return Enumerable.Repeat((byte)0xff, count).ToArray();
                },
            },
            new FixedDraws(Hex("0000"), 256));

        generator.Generate(_corpusTime);

        Assert.Equal(Expected(string.Empty, UxidSize.S, false, _corpusTime, "0100"), generator.Generate(_corpusTime));
        Assert.Equal(0, calls);
    }

    [Fact] // (sabotage: the lock in Generate removed -> Assert.True() Failure)
    public async Task IncreasesStrictlyAcrossThreads()
    {
        const int threads = 8;
        const int perThread = 5000;
        var generator = new UxidMonotonic(
            new UxidOptions { Prefix = "usr", Time = _corpusTime },
            new FixedDraws(new byte[10], 1));
        using var start = new Barrier(threads);

        var lists = Enumerable.Range(0, threads)
            .Select(_ => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    var ids = new List<string>(perThread);
                    for (var index = 0; index < perThread; index++)
                    {
                        ids.Add(generator.Generate());
                    }

                    return ids;
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();
        var results = await Task.WhenAll(lists);

        foreach (var ids in results)
        {
            for (var index = 1; index < ids.Count; index++)
            {
                Assert.True(string.CompareOrdinal(ids[index - 1], ids[index]) < 0, "each thread sees its ids increase");
            }
        }

        var all = results.SelectMany(ids => ids).ToList();
        Assert.Equal(threads * perThread, all.Distinct(StringComparer.Ordinal).Count());
    }

    private static string Expected(string prefix, UxidSize size, bool compact, long time, string rand, int? randSize = null) =>
        Uxid.Generate(new UxidOptions { Prefix = prefix, Size = size, CompactTime = compact, Time = time, Rand = Hex(rand), RandSize = randSize });

    private static byte[] Hex(string text)
    {
        var bytes = new byte[text.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(text.Substring(2 * index, 2), 16);
        }

        return bytes;
    }

    // Replays recorded draws in order: each seed as a big-endian field of
    // the width asked for, each step as given.
    private sealed class ReplayDraws(IReadOnlyList<long> seeds, IReadOnlyList<long> steps) : IUxidDraws
    {
        private int _seed;
        private int _step;

        public bool Exhausted => _seed == seeds.Count && _step == steps.Count;

        public byte[] DrawSeed(int width)
        {
            var value = seeds[_seed++];
            var field = new byte[width];
            for (var index = width - 1; index >= 0 && value > 0; index--)
            {
                field[index] = (byte)value;
                value >>= 8;
            }

            return field;
        }

        public long DrawStep(int width) => steps[_step++];
    }

    // The same seed and step on every draw; safe to share across threads.
    private sealed class FixedDraws(byte[] seed, long step) : IUxidDraws
    {
        public byte[] DrawSeed(int width) => (byte[])seed.Clone();

        public long DrawStep(int width) => step;
    }
}
