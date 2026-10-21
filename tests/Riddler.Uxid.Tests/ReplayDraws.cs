using System.Collections.Generic;

namespace Riddler.Tests;

// Replays recorded draws in order: each seed as a big-endian field of the
// width asked for, each step as given. Shared by the monotonic tests and the
// conformance corpus runner, whose monotonic cases carry their draws as
// values.
internal sealed class ReplayDraws(IReadOnlyList<long> seeds, IReadOnlyList<long> steps) : IUxidDraws
{
    private int _seed;
    private int _step;

    /// <summary>Gets whether every seed and every step has been drawn.</summary>
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
