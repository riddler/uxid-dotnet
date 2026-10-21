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

    /// <summary>
    /// Appends the first <c>5 x chars</c> bits of <paramref name="bytes"/>,
    /// most significant bit first, as <paramref name="chars"/> characters of
    /// 5 bits each, with no short group. Each character reads a 16-bit window
    /// from the byte its bits start in, so the bytes must run at least one
    /// byte past the bits written, as a 32-byte SHA-256 digest does for the
    /// widest deterministic body (125 bits).
    /// </summary>
    internal static void AppendLeadingBits(StringBuilder text, byte[] bytes, int chars, UxidCase letterCase)
    {
        var alphabet = Alphabet(letterCase);
        for (var index = 0; index < chars; index++)
        {
            var bit = 5 * index;
            var at = bit >> 3;
            var window = (bytes[at] << 8) | bytes[at + 1];
            text.Append(alphabet[(window >> (11 - (bit & 7))) & 31]);
        }
    }

    /// <summary>
    /// The 5-bit value of a symbol in either case, or -1 for any other
    /// character: the letters I, L, O and U, and every character outside the
    /// two alphabets, have none.
    /// </summary>
    internal static int Value(char symbol)
    {
        var lower = symbol is >= 'A' and <= 'Z' ? (char)(symbol + ('a' - 'A')) : symbol;
        return lower switch
        {
            >= '0' and <= '9' => lower - '0',
            >= 'a' and <= 'h' => lower - 'a' + 10,
            'j' or 'k' => lower - 'j' + 18,
            'm' or 'n' => lower - 'm' + 20,
            >= 'p' and <= 't' => lower - 'p' + 22,
            >= 'v' and <= 'z' => lower - 'v' + 27,
            _ => -1,
        };
    }

    /// <summary>
    /// Reads <paramref name="count"/> symbols from <paramref name="start"/>
    /// as one integer, 5 bits each, most significant first. The caller
    /// passes symbols only.
    /// </summary>
    internal static long ReadInteger(string text, int start, int count)
    {
        long value = 0;
        for (var index = start; index < start + count; index++)
        {
            value = (value << 5) | (long)Value(text[index]);
        }

        return value;
    }

    private static string Alphabet(UxidCase letterCase) =>
        letterCase == UxidCase.Upper ? _upperAlphabet : _lowerAlphabet;
}
