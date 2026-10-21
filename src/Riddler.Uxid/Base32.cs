using System.Collections.Generic;
using System.Text;

namespace Riddler;

/// <summary>
/// Crockford Base32 as the wire format writes it: most significant bit first,
/// 5 bits per character, and a short group written as its raw value.
/// </summary>
internal static class Base32
{
    private const string _lowerAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";
    private const string _upperAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>
    /// Appends the low <c>5 x chars</c> bits of a non-negative value as
    /// <paramref name="chars"/> characters. A 48-bit time in 10 characters
    /// starts with its 3-bit short group, whose top 2 bits are zero.
    /// </summary>
    internal static void AppendInteger(StringBuilder text, long value, int chars, UxidCase letterCase)
    {
        var alphabet = Alphabet(letterCase);
        for (var group = chars - 1; group >= 0; group--)
        {
            text.Append(alphabet[(int)((value >> (5 * group)) & 31)]);
        }
    }

    /// <summary>
    /// Appends bytes as <c>ceil(8 x count / 5)</c> characters, the last one a
    /// short group's raw value (not shifted or padded) when the bit count is
    /// not a multiple of 5.
    /// </summary>
    internal static void AppendBytes(StringBuilder text, IReadOnlyList<byte> bytes, UxidCase letterCase)
    {
        var alphabet = Alphabet(letterCase);
        var buffer = 0;
        var bits = 0;
        for (var index = 0; index < bytes.Count; index++)
        {
            buffer = (buffer << 8) | bytes[index];
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                text.Append(alphabet[(buffer >> bits) & 31]);
            }

            buffer &= (1 << bits) - 1;
        }

        if (bits > 0)
        {
            text.Append(alphabet[buffer]);
        }
    }

    private static string Alphabet(UxidCase letterCase) =>
        letterCase == UxidCase.Upper ? _upperAlphabet : _lowerAlphabet;
}
