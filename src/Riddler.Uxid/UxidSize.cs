namespace Riddler;

/// <summary>
/// An id's size, which fixes the random field's width, and so the body
/// length, for standard and for compact time.
/// </summary>
/// <remarks>
/// <para>
/// Random bytes and body lengths per size, in standard and compact time:
/// <see cref="Xs"/> 0 and 1 bytes, 10 and 10 characters; <see cref="S"/>
/// 2 and 3 bytes, 14 and 13 characters; <see cref="M"/> 5 and 6 bytes, 18
/// and 18 characters; <see cref="L"/> 7 and 8 bytes, 22 and 21 characters;
/// <see cref="Xl"/> 10 and 10 bytes, 26 and 24 characters.
/// </para>
/// <para>
/// <see cref="Uxid.ParseSize(string)"/> reads a size from its short spelling
/// (<c>xs</c>, <c>s</c>, <c>m</c>, <c>l</c>, <c>xl</c>) or its long one
/// (<c>xsmall</c>, <c>small</c>, <c>medium</c>, <c>large</c>,
/// <c>xlarge</c>). The default size of <see cref="UxidOptions.Size"/> is
/// <see cref="Xl"/>.
/// </para>
/// </remarks>
public enum UxidSize
{
    /// <summary>Extra small (<c>xs</c> or <c>xsmall</c>): no random field in standard time.</summary>
    Xs,

    /// <summary>Small (<c>s</c> or <c>small</c>).</summary>
    S,

    /// <summary>Medium (<c>m</c> or <c>medium</c>).</summary>
    M,

    /// <summary>Large (<c>l</c> or <c>large</c>).</summary>
    L,

    /// <summary>Extra large (<c>xl</c> or <c>xlarge</c>), the default.</summary>
    Xl,
}
