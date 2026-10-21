### Added

- `UxidOptions.From` makes `Uxid.Generate` build a deterministic id: the marker `z` and a SHA-256 hash of the prefix, the string and the size, so the same prefix, string and size always give the same id, for example `usr_zcvt7epac0t1ebcsjfyf7cwz25` from `alice@example.com` at size `Xl`.
- `Uxid.Generate` refuses fixed random bytes (`UxidOptions.Rand`) beside `From` with `UxidErrorCode.RandWithFrom` (`rand_with_from`).
