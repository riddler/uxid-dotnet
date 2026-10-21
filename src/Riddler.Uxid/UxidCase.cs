namespace Riddler;

/// <summary>
/// The letter case of an id's body. Case never applies to the prefix, and an
/// upper-case body and its lower-case spelling are the same id.
/// </summary>
public enum UxidCase
{
    /// <summary>Lower case, the default.</summary>
    Lower,

    /// <summary>Upper case.</summary>
    Upper,
}
