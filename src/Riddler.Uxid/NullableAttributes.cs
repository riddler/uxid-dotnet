#if !NET5_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis;

/// <summary>
/// Lets the compiler read a nullable out parameter as set when the method
/// returns the given value, on netstandard2.0, whose base library does not
/// declare this type; net10.0 declares it and this copy is not compiled
/// there.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
internal sealed class NotNullWhenAttribute : Attribute
{
    /// <summary>Initializes the attribute with the return value it names.</summary>
    /// <param name="returnValue">The return value for which the parameter is not null.</param>
    public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

    /// <summary>Gets the return value for which the parameter is not null.</summary>
    public bool ReturnValue { get; }
}

/// <summary>
/// Lets the compiler read the named members as set when a property or method
/// returns the given value, on netstandard2.0, whose base library does not
/// declare this type; net10.0 declares it and this copy is not compiled
/// there.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
internal sealed class MemberNotNullWhenAttribute : Attribute
{
    /// <summary>Initializes the attribute with the return value and the members it names.</summary>
    /// <param name="returnValue">The return value for which the members are not null.</param>
    /// <param name="members">The names of the members that are not null.</param>
    public MemberNotNullWhenAttribute(bool returnValue, params string[] members)
    {
        ReturnValue = returnValue;
        Members = members;
    }

    /// <summary>Gets the return value for which the members are not null.</summary>
    public bool ReturnValue { get; }

    /// <summary>Gets the names of the members that are not null.</summary>
    public string[] Members { get; }
}
#endif
