# Changelog

## 1.0.0 - 2026-10-21

The first release of `Riddler.Uxid`, the .NET port of uxid: prefixed,
K-sortable identifiers a person can read, wire-compatible with the Elixir
`uxid` package. It covers time-based generation, deterministic ids, decoding,
the validity checks and the monotonic generator; it targets `netstandard2.0`
and `net10.0` with no package dependencies; and it passes every case of the
uxid conformance corpus at uxid-ex's tag `v2.10.0`, vendored byte for byte
and run with no skip list.

### Added

- `UxidMonotonic` generates ids that increase strictly from one generator, also when several fall in one millisecond and when the clock moves back, following the wire format's monotonic rule; it is built from `UxidOptions`, keeps its state in the instance, and is safe for concurrent use.
- `IUxidDraws` lets a `UxidMonotonic` replay known seeds and steps in place of its byte source, for reproducible vectors such as the conformance corpus's.
- A `UxidMonotonic` refuses `UxidOptions.From` with `UxidErrorCode.FromWithMonotonic` (`from_with_monotonic`) and `UxidOptions.Rand` with `UxidErrorCode.RandWithMonotonic` (`rand_with_monotonic`).
- `Uxid.Decode` reads an id back to a `UxidDecodeResult`: either a `UxidDecoded` (its `Prefix`, `Size`, `IsCompact`, `IsDeterministic` and `Time`) or a `UxidDecodeError` (`Empty`, `InvalidLength` or `InvalidCharacter`, checked in that order) with its wire-format name in `ErrorName`. It never throws on a malformed string; a null string or a null or empty delimiter throws.
- `UxidDecodeOptions` takes the delimiter (default `_`) and the decoder's clock, a function returning Unix milliseconds (default the system clock), which supplies the high bits of a compact time; with a clock less than 2^40 ms after the Unix epoch, a compact time can decode to a negative value.
- `Uxid.TryDecode` answers whether a string decodes under the delimiter `_` and the system clock, with the decoded value as an out parameter.
- `Uxid.IsValid` answers whether a string is a well-formed id under a delimiter, with an optional required prefix; `Uxid.IsDeterministic` answers whether it is a well-formed deterministic one.
- `Uxid.Generate` builds a time-based id from a `UxidOptions`: a prefix (the empty string is no prefix), a `UxidSize` from `Xs` to `Xl` (default `Xl`), an explicit random field width (`RandSize`), compact time, a `UxidCase`, a delimiter (default `_`) and a time in milliseconds since the Unix epoch (default the system clock).
- `UxidOptions.ByteSource` takes the random bytes from a function of a count to a byte array; the default is `RandomNumberGenerator`.
- `UxidOptions.Rand` fixes the random field's bytes for reproducible vectors such as tests; it is never a source of randomness.
- `Uxid.ParseSize` reads a size from its short spelling (`xs`, `s`, `m`, `l`, `xl`) or its long one (`xsmall`, `small`, `medium`, `large`, `xlarge`).
- A refused request throws `UxidException`, an `ArgumentException` whose `Code` is a `UxidErrorCode` and whose `CodeName` is the wire format's name for it: `compact_time_out_of_range`, `rand_width_mismatch`, `rand_size_too_large`, `from_with_monotonic`, `rand_with_from` or `rand_with_monotonic`.
- `UxidOptions.From` makes `Uxid.Generate` build a deterministic id: the marker `z` and a SHA-256 hash of the prefix, the string and the size, so the same prefix, string and size always give the same id, for example `usr_zcvt7epac0t1ebcsjfyf7cwz25` from `alice@example.com` at size `Xl`.
- `Uxid.Generate` refuses fixed random bytes (`UxidOptions.Rand`) beside `From` with `UxidErrorCode.RandWithFrom` (`rand_with_from`).
- For a deterministic id, `Uxid.Generate` refuses a prefix or a `UxidOptions.From` that is not well-formed UTF-16 (one holding a lone surrogate, which has no UTF-8 bytes to hash) with an `ArgumentException`, not a `UxidException`.
