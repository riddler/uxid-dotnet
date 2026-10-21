#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets the compiler emit <c>init</c> accessors on netstandard2.0, whose
/// base library does not declare this type; net10.0 declares it and this
/// copy is not compiled there.
/// </summary>
internal static class IsExternalInit
{
}
#endif
