# Riddler.Uxid

[![MIT License][badge_license_url]](LICENSE)
[![CI][badge_ci_url]](https://github.com/riddler/uxid-dotnet/actions/workflows/ci.yml)

User eXperience focused IDentifiers for .NET: prefixed, K-sortable,
Stripe-style ids a person can read, copy and route back to their resource. An
id looks like `usr_01hf7yat0004h1`: the prefix names the resource, and the
body carries a time and randomness in lower-case Crockford Base32. The ids
follow the wire format of the Elixir reference implementation,
[uxid-ex](https://github.com/riddler/uxid-ex), and the package is checked
against that implementation's conformance corpus, every case, with no skip
list.

## Why UXID

An auto-increment key leaks how many rows a table holds and invites
enumeration; a random GUID fixes both but is long, unordered and anonymous, so
in a log line, a URL or a support thread nobody can tell what it points at, and
a double-click selects only part of it. A UXID names its resource in its
prefix, selects whole on a double-click, reads aloud without ambiguity, sorts
by creation time so it indexes well, and is generated in the application with
no coordination between nodes. Its size is tunable for low-cardinality
resources, a monotonic generator keeps a burst within one millisecond unique
and in order, and a deterministic mode maps the same input to the same id.

Because the format is shared, an id this package makes is read by the other
implementations of the format, and the other way round.

## Install

```bash
dotnet add package Riddler.Uxid
```

## Basic usage

```csharp
using System;
using Riddler;

// A prefix names the resource; the body is a time and randomness.
var id = Uxid.Generate(new UxidOptions { Prefix = "usr" });
Console.WriteLine(id.StartsWith("usr_", StringComparison.Ordinal)); // True

// Shrink the random part for low-cardinality resources.
// Sizes: Xs, S, M, L and Xl (the default).
var small = Uxid.Generate(new UxidOptions { Prefix = "cus", Size = UxidSize.S });
Console.WriteLine(small.Length == "cus_".Length + 14); // True

// Deterministic: the same prefix and input give the same id, every time.
var named = Uxid.Generate(new UxidOptions { Prefix = "usr", From = "alice@example.com" });
Console.WriteLine(named); // usr_zcvt7epac0t1ebcsjfyf7cwz25
Console.WriteLine(Uxid.IsDeterministic(named, "_")); // True

// Read an id back. A malformed id is a result with an error, never an exception.
var decoded = Uxid.Decode("cus_01hf7yat0004h1");
Console.WriteLine(decoded.IsSuccess); // True
Console.WriteLine(decoded.Value!.Prefix); // cus
Console.WriteLine(decoded.Value.Size); // S
Console.WriteLine(decoded.Value.Time); // 1700000000000
Console.WriteLine(Uxid.IsValid("cus_01hf7yat0004h1", "_", "cus")); // True
Console.WriteLine(Uxid.Decode("cus_").ErrorName); // empty

// Monotonic: ids from one generator increase strictly, also within one
// millisecond. You hold the generator; the package keeps no global state.
var ids = new UxidMonotonic(new UxidOptions { Prefix = "usr", Size = UxidSize.M });
var first = ids.Generate();
var second = ids.Generate();
Console.WriteLine(string.CompareOrdinal(first, second) < 0); // True
```

A generation request the wire format refuses, such as a random width above 10
bytes, throws a `UxidException`, an `ArgumentException`; switch on its `Code`,
a `UxidErrorCode`, or log its `CodeName`, a stable `snake_case` name, rather
than its message. A malformed argument of another kind, such as a null
prefix or a time outside the 48-bit range, throws the usual
`ArgumentException` or `ArgumentOutOfRangeException`.

`Uxid.Generate` and `UxidMonotonic.Generate()` read the system clock when they
need a time, and the platform's cryptographic random source
(`RandomNumberGenerator`) when they need random bytes. `UxidOptions.Time` and
`UxidOptions.ByteSource` replace them; `UxidDecodeOptions.Clock` replaces the
clock `Uxid.Decode` reads for a compact time. `UxidOptions.Rand` takes fixed
random bytes for test vectors: an id built from them is exactly as guessable
as those bytes.

`Uxid.IsValid(id, delimiter, requiredPrefix)` and
`Uxid.IsDeterministic(id, delimiter)` take the delimiter the id was
generated with, and read no clock. `IsValid` answers whether the id decodes
(a body of a length the format defines, in Crockford Base32 characters) and,
given a required prefix, whether it carries exactly that prefix.
`IsDeterministic` answers whether the id decodes and its body starts with the
marker `z` or `Z`. Both are stricter than the Elixir reference, whose
`valid?` accepts any alphabet body of at least 8 characters and whose
`deterministic?` checks only the first body character: here a malformed
string is neither valid nor deterministic. The released TypeScript and Rust
ports read them the same way, and the conformance corpus does not fix these
two answers. `Uxid.TryDecode` is `Decode` with the default options, without
the error.

`Uxid.Generate` with `From` refuses a prefix or input that is not well-formed
UTF-16 (a lone surrogate) with an `ArgumentException`, because such a string
has no UTF-8 bytes to hash.

## Compatibility

- **.NET 10 and every runtime that implements netstandard2.0.** The package
  targets `net10.0` and `netstandard2.0`, so it runs on .NET Framework 4.6.1
  and later, .NET Core 2.0 and later, and every later .NET. CI proves the
  floor on Windows by running the tests on .NET Framework 4.8.1 against the
  `netstandard2.0` build, and runs them on `net10.0`.
- **No dependencies.** The package has no package reference on either target;
  its surface uses byte arrays and `Func` delegates rather than `Span` and
  `TimeProvider`, so nothing beyond the framework is pulled in.
- **Thread safety.** A `UxidMonotonic` is safe for concurrent use: a private
  lock object inside the instance orders its calls, so its ids increase
  strictly in the order the calls take the lock, whichever thread asks. Two
  generators never affect each other, and ids from two generators carry no
  order between them within a millisecond. `Uxid.Generate`, `Uxid.Decode` and
  the validity checks keep no state and can be called from any thread.

## Documentation

- Look up
  - The API reference is the XML documentation shipped with the package:
    every public type and member carries it, and a missing comment fails the
    build.
  - [The wire format](docs/wire-format.md): where the normative layout of an
    id lives, and which version of it this package follows.
  - [The changelog](CHANGELOG.md): what changed in each version.
  - [The conformance corpus](conformance): the cases every implementation of
    the wire format passes, vendored here from uxid-ex.
- Understand
  - [The reference spec, at v2.10.0][guide_wire_format_url]: the normative
    layout of an id - alphabet, bit packing, sizes, prefix grammar,
    deterministic and monotonic bodies, and decoding.
  - [Why a UXID has a prefix, a time and randomness][guide_why_url]: what each
    part of an id is for, and what you trade when you tune it.
  - [Why monotonic mode exists and what it costs][guide_monotonic_url]:
    same-millisecond uniqueness and ordering, and the security tradeoff.
  - [_Designing APIs for humans: object IDs_][stripe_ids_url]: the Stripe id
    design many of UXID's choices follow.

## Development

After checking out the repository, run `mise install` for the .NET SDK
`mise.toml` pins. Then `mise run gate:loop` runs the inner loop (build and
tests), and `mise run gate` runs the full quality gate: the format check, the
build with the analyzers on and every warning an error, the tests with 90
percent line coverage, the package build with validation, and the
conformance corpus check. The tests include the conformance corpus, every case
with no skip list.

A release is published by CI, never from a checkout: pushing a tag `vX.Y.Z`
that names the version in `src/Riddler.Uxid/Riddler.Uxid.csproj` runs
`.github/workflows/release.yml`, which publishes the package to
[nuget.org](https://www.nuget.org).

## Contributing

Bug reports and pull requests are welcome on GitHub at
https://github.com/riddler/uxid-dotnet.

## License

The package is available as open source under the terms of the
[MIT License](https://opensource.org/licenses/MIT).

<!-- LINKS -->
[guide_wire_format_url]: https://github.com/riddler/uxid-ex/blob/v2.10.0/guides/wire-format.md
[guide_why_url]: https://github.com/riddler/uxid-ex/blob/v2.10.0/guides/why-prefix-time-randomness.md
[guide_monotonic_url]: https://github.com/riddler/uxid-ex/blob/v2.10.0/guides/monotonic.md
[stripe_ids_url]: https://dev.to/stripe/designing-apis-for-humans-object-ids-3o5a

<!-- BADGES -->
[badge_license_url]: https://img.shields.io/badge/license-MIT-brightgreen.svg
[badge_ci_url]: https://github.com/riddler/uxid-dotnet/actions/workflows/ci.yml/badge.svg
