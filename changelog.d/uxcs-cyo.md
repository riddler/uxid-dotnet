### Added

- `Uxid.Generate` builds a time-based id from a `UxidOptions`: a prefix (the empty string is no prefix), a `UxidSize` from `Xs` to `Xl` (default `Xl`), an explicit random field width (`RandSize`), compact time, a `UxidCase`, a delimiter (default `_`) and a time in milliseconds since the Unix epoch (default the system clock).
- `UxidOptions.ByteSource` takes the random bytes from a function of a count to a byte array; the default is `RandomNumberGenerator`.
- `UxidOptions.Rand` fixes the random field's bytes for reproducible vectors such as tests; it is never a source of randomness.
- `Uxid.ParseSize` reads a size from its short spelling (`xs`, `s`, `m`, `l`, `xl`) or its long one (`xsmall`, `small`, `medium`, `large`, `xlarge`).
- A refused request throws `UxidException`, an `ArgumentException` whose `Code` is a `UxidErrorCode` and whose `CodeName` is the wire format's name for it: `compact_time_out_of_range`, `rand_width_mismatch`, `rand_size_too_large`, `from_with_monotonic`, `rand_with_from` or `rand_with_monotonic`.
