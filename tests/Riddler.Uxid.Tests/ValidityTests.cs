using System;
using Xunit;

namespace Riddler.Tests;

// The validity checks: whether a string is a well-formed id under a
// delimiter, with or without a required prefix, and whether it is a
// well-formed deterministic one. Both answer what decoding would, and read
// no clock.
public class ValidityTests
{
    [Theory] // (sabotage: required prefix ignored -> Assert.Equal() Failure: Values differ)
    [InlineData("cus_01hf7yat0004h1", "_", null, true)]
    [InlineData("cus_01hf7yat0004h1", "_", "", true)]
    [InlineData("cus_01hf7yat0004h1", "_", "cus", true)]
    [InlineData("cus_01hf7yat0004h1", "_", "usr", false)]
    [InlineData("cus_01hf7yat0004h1", "_", "CUS", false)]
    [InlineData("01hf7yat0004h1", "_", null, true)]
    [InlineData("01hf7yat0004h1", "_", "cus", false)]
    [InlineData("a_b_01hf7yat0004h1", "_", "a_b", true)]
    [InlineData("a_b_01hf7yat0004h1", "_", "a", false)]
    [InlineData("cus--01hf7yat0004h1", "--", "cus", true)]
    [InlineData("cus--01hf7yat0004h1", "_", null, false)]
    [InlineData("cus_hf7yat0004hm5", "_", "cus", true)]
    [InlineData("cus_zt9vnyfd85bff0", "_", "cus", true)]
    [InlineData("cus_01hf7yat0004h12", "_", null, false)]
    [InlineData("cus_01hf7yat0004hl", "_", null, false)]
    [InlineData("cus_", "_", "cus", false)]
    [InlineData("", "_", null, false)]
    [InlineData(null, "_", null, false)]
    public void IsValidAnswersWhetherTheIdDecodesUnderThePrefix(string? id, string delimiter, string? requiredPrefix, bool expected)
    {
        Assert.Equal(expected, Uxid.IsValid(id, delimiter, requiredPrefix));
    }

    [Fact] // (sabotage: IsValid splits at _ whatever the delimiter -> Assert.True() Failure)
    public void IsValidTakesThePrefixAsOptional()
    {
        Assert.True(Uxid.IsValid("cus::01hf7yat0004h1", "::"));
        Assert.False(Uxid.IsValid("cus::01hf7yat0004h1", "_"));
    }

    [Theory] // (sabotage: marker checked without the well-formed check -> Assert.Equal() Failure: Values differ)
    [InlineData("usr_zcvt7epac0t1ebcsjfyf7cwz25", "_", true)]
    [InlineData("usr_ZCVT7EPAC0T1EBCSJFYF7CWZ25", "_", true)]
    [InlineData("zqz4cwzgyw8ks97869", "_", true)]
    [InlineData("usr--zcvt7epac0t1ebcsjfyf7cwz25", "--", true)]
    [InlineData("usr--zcvt7epac0t1ebcsjfyf7cwz25", "_", false)]
    [InlineData("usr_01hf7yat0004h1", "_", false)]
    [InlineData("usr_zcvt7epac", "_", false)]
    [InlineData("usr_zcvt7epac0t1ebcsjfyf7cwzi5", "_", false)]
    [InlineData("usr_", "_", false)]
    [InlineData(null, "_", false)]
    public void IsDeterministicAnswersWhetherTheIdDecodesWithTheMarker(string? id, string delimiter, bool expected)
    {
        Assert.Equal(expected, Uxid.IsDeterministic(id, delimiter));
    }

    [Theory] // (sabotage: IsValid names the wrong parameter -> Assert.Equal() Failure: Strings differ)
    [InlineData("")]
    [InlineData(null)]
    public void ChecksRefuseAMissingDelimiter(string? delimiter)
    {
        Assert.Equal("delimiter", Assert.Throws<ArgumentException>(() => Uxid.IsValid("usr_01hf7yat00", delimiter!)).ParamName);
        Assert.Equal("delimiter", Assert.Throws<ArgumentException>(() => Uxid.IsDeterministic("usr_01hf7yat00", delimiter!)).ParamName);
    }
}
