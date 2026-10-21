### Added

- `Uxid.Decode` reads an id back to a `UxidDecodeResult`: either a `UxidDecoded` (its `Prefix`, `Size`, `IsCompact`, `IsDeterministic` and `Time`) or a `UxidDecodeError` (`Empty`, `InvalidLength` or `InvalidCharacter`, checked in that order) with its wire-format name in `ErrorName`. It never throws on a malformed string; a null string or a null or empty delimiter throws.
- `UxidDecodeOptions` takes the delimiter (default `_`) and the decoder's clock, a function returning Unix milliseconds (default the system clock), which supplies the high bits of a compact time; with a clock less than 2^40 ms after the Unix epoch, a compact time can decode to a negative value.
- `Uxid.TryDecode` answers whether a string decodes under the delimiter `_` and the system clock, with the decoded value as an out parameter.
- `Uxid.IsValid` answers whether a string is a well-formed id under a delimiter, with an optional required prefix; `Uxid.IsDeterministic` answers whether it is a well-formed deterministic one.
