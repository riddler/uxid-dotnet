using Xunit;

namespace Riddler.Tests;

// Every member of the public enums has a fixed numeric value, so a host that
// stores one as an integer reads the same member back from any later version.
public class EnumValueTests
{
    [Theory] // (sabotage: UxidSize.M renumbered to 5 -> Assert.Equal() Failure: Values differ)
    [InlineData(UxidSize.Xs, 0)]
    [InlineData(UxidSize.S, 1)]
    [InlineData(UxidSize.M, 2)]
    [InlineData(UxidSize.L, 3)]
    [InlineData(UxidSize.Xl, 4)]
    public void SizeValuesAreFixed(UxidSize size, int expected)
    {
        Assert.Equal(expected, (int)size);
    }

    [Theory] // (sabotage: UxidCase values swapped -> Assert.Equal() Failure: Values differ)
    [InlineData(UxidCase.Lower, 0)]
    [InlineData(UxidCase.Upper, 1)]
    public void CaseValuesAreFixed(UxidCase letterCase, int expected)
    {
        Assert.Equal(expected, (int)letterCase);
    }

    [Theory] // (sabotage: UxidErrorCode.RandWithFrom renumbered to 9 -> Assert.Equal() Failure: Values differ)
    [InlineData(UxidErrorCode.CompactTimeOutOfRange, 0)]
    [InlineData(UxidErrorCode.RandWidthMismatch, 1)]
    [InlineData(UxidErrorCode.RandSizeTooLarge, 2)]
    [InlineData(UxidErrorCode.FromWithMonotonic, 3)]
    [InlineData(UxidErrorCode.RandWithFrom, 4)]
    [InlineData(UxidErrorCode.RandWithMonotonic, 5)]
    public void ErrorCodeValuesAreFixed(UxidErrorCode code, int expected)
    {
        Assert.Equal(expected, (int)code);
    }
}
