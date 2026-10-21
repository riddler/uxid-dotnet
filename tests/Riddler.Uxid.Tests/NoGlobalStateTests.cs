using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Riddler.Tests;

// The library keeps no global state: no static field it can reassign, so no
// counter, cache or configuration shared between callers. The monotonic
// generator keeps its state in an instance the caller holds. The check reads
// every type the library assembly declares, at any accessibility, and skips
// the types the compiler generates (lambda caches and the like); the backing
// field of a static auto-property is checked like any other field. A static
// readonly field holding a mutable object is beyond what reflection can judge
// here.
public class NoGlobalStateTests
{
    [Fact] // (sabotage: a private static int field in an internal library class -> Assert.Empty() Failure: Collection was not empty)
    public void LibraryDeclaresNoReassignableStaticField()
    {
        var library = Assembly.Load(new AssemblyName("Riddler.Uxid"));

        var reassignable = library.GetTypes()
            .Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            .SelectMany(type => type.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(field => !field.IsInitOnly && !field.IsLiteral)
            .Select(field => field.DeclaringType + "." + field.Name)
            .ToList();

        Assert.Empty(reassignable);
    }
}
