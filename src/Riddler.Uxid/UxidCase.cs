namespace Riddler;

/// <summary>
/// The letter case of an id's body. Case never applies to the prefix, and an
/// upper-case body and its lower-case spelling are the same id. Each
/// member's numeric value is fixed, so a stored integer keeps its meaning.
/// </summary>
public enum UxidCase
{
    /// <summary>Lower case, the default.</summary>
    Lower = 0,

    /// <summary>Upper case.</summary>
    Upper = 1,
}
