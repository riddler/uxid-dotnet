using System;
using System.Text;
using Xunit;

namespace Riddler.Tests;

// Deterministic ids against the wire format. The vectors are the conformance
// corpus's deterministic cases and its rand-with-from refusal (uxid-ex at
// v2.10.0, conformance/deterministic.json and conformance/invalid.json),
// copied as their inputs and expected strings, each distinct input once;
// a null prefix in the corpus is no prefix, written here as the empty
// string. The full corpus runner replaces them as the arbiter once the
// corpus is vendored.
public class DeterministicTests
{
    private const string _from = "alice@example.com";

    [Theory] // (sabotage: count byte left out of the hash input -> Assert.Equal() Failure: Strings differ)
    [InlineData("usr", "xl", "lower", "alice@example.com", "usr_zcvt7epac0t1ebcsjfyf7cwz25")]
    [InlineData("cus", "s", "lower", "acme-corp", "cus_zt9vnyfd85bff0")]
    [InlineData("", "m", "lower", "alice@example.com", "zqz4cwzgyw8ks97869")]
    [InlineData("usr", "xs", "lower", "alice@example.com", "usr_z1yhw20vag")]
    [InlineData("usr", "s", "lower", "alice@example.com", "usr_z5rtsrkhx2h4dr")]
    [InlineData("usr", "m", "lower", "alice@example.com", "usr_zt8naccefxhwnrmscd")]
    [InlineData("usr", "l", "lower", "alice@example.com", "usr_zwbz6myvgz7ehe3nceap5n")]
    [InlineData("", "xl", "lower", "alice@example.com", "z4krgegf6drn563pvqhya4tsx9")]
    [InlineData("usr", "xl", "upper", "alice@example.com", "usr_ZCVT7EPAC0T1EBCSJFYF7CWZ25")]
    [InlineData("cus", "m", "lower", "café", "cus_zfsx9se75671f9kdjv")]
    public void DerivesCorpusVectors(string prefix, string size, string letterCase, string from, string expected)
    {
        var options = new UxidOptions
        {
            Prefix = prefix,
            Size = Uxid.ParseSize(size),
            Case = letterCase == "upper" ? UxidCase.Upper : UxidCase.Lower,
            From = from,
        };

        Assert.Equal(expected, Uxid.Generate(options));
    }

    [Theory] // (sabotage: Rand beside From not refused -> Assert.Throws() Failure: No exception was thrown)
    [InlineData("00010203040506070809")]
    [InlineData("")]
    public void RefusesFixedRandWithFrom(string? rand)
    {
        var options = new UxidOptions { Prefix = "usr", From = _from, Rand = Hex(rand) };

        var refusal = Assert.Throws<UxidException>(() => Uxid.Generate(options));

        Assert.Equal(UxidErrorCode.RandWithFrom, refusal.Code);
        Assert.Equal("rand_with_from", refusal.CodeName);
    }

    [Fact] // (sabotage: From branch after the time and width checks -> Assert.Null() Failure: Value is not null)
    public void ReadsNoTimeWidthOrByteSourceForFrom()
    {
        var options = new UxidOptions
        {
            Prefix = "usr",
            From = _from,
            Time = -1,
            CompactTime = true,
            RandSize = 11,
            ByteSource = _ => throw new InvalidOperationException("The byte source was called."),
        };

        string? id = null;

        var refusal = Record.Exception(() => id = Uxid.Generate(options));

        Assert.Null(refusal);
        Assert.Equal("usr_zcvt7epac0t1ebcsjfyf7cwz25", id);
    }

    [Fact] // (sabotage: deterministic id joined with _ always -> Assert.Equal() Failure: Strings differ)
    public void JoinsPrefixAndDeterministicBodyWithTheDelimiter()
    {
        var options = new UxidOptions { Prefix = "usr", Delimiter = "--", From = _from };

        Assert.Equal("usr--zcvt7epac0t1ebcsjfyf7cwz25", Uxid.Generate(options));
    }

    [Fact] // (sabotage: empty From read as none -> Assert.Equal() Failure: Strings differ)
    public void DerivesAnIdFromTheEmptyString()
    {
        var options = new UxidOptions { Prefix = "usr", From = string.Empty };

        Assert.Equal("usr_z5006zc7jgfjwsjagnepj46p6x", Uxid.Generate(options));
    }

    // An attribute argument is stored as UTF-8, where a lone surrogate cannot
    // be written, so the strings are built in the test body.
    [Theory] // (sabotage: replacing UTF-8 encoder in place of the strict one -> Assert.Throws() Failure: No exception was thrown)
    [InlineData(false)]
    [InlineData(true)]
    public void RefusesALoneSurrogate(bool inPrefix)
    {
        var options = inPrefix
            ? new UxidOptions { Prefix = "\udc00", From = _from }
            : new UxidOptions { Prefix = "usr", From = "\ud800" };

        var refusal = Assert.Throws<ArgumentException>(() => Uxid.Generate(options));

        Assert.Equal("options", refusal.ParamName);
        Assert.IsType<EncoderFallbackException>(refusal.InnerException);
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
}
