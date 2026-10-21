using System;
using System.IO;
using Xunit;

namespace Riddler.Tests;

// Every case of the vendored conformance corpus (conformance/, uxid-ex at the
// tag conformance/SOURCE.json names), one theory per case file, with no skip
// list: a case this port cannot pass is reported against the reference, never
// skipped. CorpusRunner runs one case; CorpusHashTests checks the files are
// the ones the manifest describes.
public class CorpusTests
{
    public static TheoryData<string, string> EncodeCases => Corpus.Cases("encode.json");

    public static TheoryData<string, string> DeterministicCases => Corpus.Cases("deterministic.json");

    public static TheoryData<string, string> DecodeCases => Corpus.Cases("decode.json");

    public static TheoryData<string, string> InvalidCases => Corpus.Cases("invalid.json");

    public static TheoryData<string, string> MonotonicCases => Corpus.Cases("monotonic.json");

    [Theory] // (sabotage: upper case read as lower -> Assert.Equal() Failure: Strings differ)
    [MemberData(nameof(EncodeCases))]
    public void EncodeCase(string id, string line)
    {
        Assert.StartsWith("encode/", id, StringComparison.Ordinal);

        CorpusRunner.Encode(line);
    }

    [Theory] // (sabotage: from gets a suffix -> Assert.Equal() Failure: Strings differ)
    [MemberData(nameof(DeterministicCases))]
    public void DeterministicCase(string id, string line)
    {
        Assert.StartsWith("deterministic/", id, StringComparison.Ordinal);

        CorpusRunner.Deterministic(line);
    }

    [Theory] // (sabotage: decoder clock at the window end -> Assert.Equal() Failure: Values differ)
    [MemberData(nameof(DecodeCases))]
    public void DecodeCase(string id, string line)
    {
        Assert.StartsWith("decode/", id, StringComparison.Ordinal);

        CorpusRunner.Decode(line);
    }

    [Theory] // (sabotage: decode refusal compared to a fixed name -> Assert.Equal() Failure: Strings differ)
    [MemberData(nameof(InvalidCases))]
    public void InvalidCase(string id, string line)
    {
        Assert.StartsWith("invalid/", id, StringComparison.Ordinal);

        CorpusRunner.Invalid(line);
    }

    [Theory] // (sabotage: times replayed in reverse -> Assert.Equal() Failure: Collections differ)
    [MemberData(nameof(MonotonicCases))]
    public void MonotonicCase(string id, string line)
    {
        Assert.StartsWith("monotonic/", id, StringComparison.Ordinal);

        CorpusRunner.Monotonic(line);
    }

    [Theory] // (sabotage: Done never reports an unread member -> Assert.Throws() Failure: No exception was thrown)
    [InlineData("""{"expected":"hf7yat0001","extra":1,"id":"encode/x","input":{"case":"lower","compact":true,"prefix":null,"rand":"01","size":"xs","time":1700000000000}}""", "encode")]
    [InlineData("""{"expected":"hf7yat0001","id":"encode/x","input":{"case":"lower","compact":true,"extra":null,"prefix":null,"rand":"01","size":"xs","time":1700000000000}}""", "encode")]
    [InlineData("""{"expected":"z","extra":[],"id":"deterministic/x","input":{"case":"lower","from":"a","prefix":null,"size":"xs"}}""", "deterministic")]
    [InlineData("""{"expected":{"compact":false,"deterministic":false,"prefix":null,"size":"xs","time":0,"extra":true},"id":"decode/x","input":"0000000000"}""", "decode")]
    [InlineData("""{"expected_error":"empty","id":"invalid/x","input":{"decode":"","extra":1}}""", "invalid")]
    [InlineData("""{"expected_error":"empty","id":"invalid/x","input":{"generate":{"extra":1,"from":"a","monotonic":true}}}""", "invalid")]
    [InlineData("""{"expected":["hf7yat0000"],"extra":1,"id":"monotonic/x","input":{"compact":true,"initial_seed":0,"overflow_seed":null,"prefix":null,"size":"xs","steps":[],"times":[1700000000000]}}""", "monotonic")]
    public void RejectsACaseWithAKeyItDoesNotKnow(string line, string kind)
    {
        Action run = kind switch
        {
            "encode" => () => CorpusRunner.Encode(line),
            "deterministic" => () => CorpusRunner.Deterministic(line),
            "decode" => () => CorpusRunner.Decode(line),
            "invalid" => () => CorpusRunner.Invalid(line),
            _ => () => CorpusRunner.Monotonic(line),
        };

        var failure = Assert.Throws<InvalidDataException>(run);

        Assert.Contains("unknown member", failure.Message, StringComparison.Ordinal);
    }

    [Theory] // (sabotage: missing member read as undefined -> Assert.Throws() Failure: Exception type was not an exact match)
    [InlineData("""{"expected":"hf7yat0001","id":"encode/x","input":{"case":"lower","compact":true,"prefix":null,"size":"xs","time":1700000000000}}""", "encode", "rand")]
    [InlineData("""{"expected":"z","id":"deterministic/x","input":{"case":"lower","prefix":null,"size":"xs"}}""", "deterministic", "from")]
    [InlineData("""{"expected":{"compact":false,"deterministic":false,"prefix":null,"size":"xs"},"id":"decode/x","input":"0000000000"}""", "decode", "time")]
    [InlineData("""{"id":"invalid/x","input":{"decode":""}}""", "invalid", "expected_error")]
    [InlineData("""{"expected":["hf7yat0000"],"id":"monotonic/x","input":{"compact":true,"initial_seed":0,"overflow_seed":null,"prefix":null,"size":"xs","times":[1700000000000]}}""", "monotonic", "steps")]
    public void RejectsACaseWithAMissingKey(string line, string kind, string missing)
    {
        Action run = kind switch
        {
            "encode" => () => CorpusRunner.Encode(line),
            "deterministic" => () => CorpusRunner.Deterministic(line),
            "decode" => () => CorpusRunner.Decode(line),
            "invalid" => () => CorpusRunner.Invalid(line),
            _ => () => CorpusRunner.Monotonic(line),
        };

        var failure = Assert.Throws<InvalidDataException>(run);

        Assert.Contains("missing member " + missing, failure.Message, StringComparison.Ordinal);
    }

    [Theory] // (sabotage: a null prefix read as the text "null" -> Assert.Equal() Failure: Strings differ)
    [InlineData("null")]
    [InlineData("\"\"")]
    public void ReadsANullPrefixAsNoPrefix(string prefix)
    {
        var line = """{"expected":"hf7yat0001","id":"encode/x","input":{"case":"lower","compact":true,"prefix":""" + prefix
            + ""","rand":"01","size":"xs","time":1700000000000}}""";

        CorpusRunner.Encode(line);
    }
}
