### Added

- `UxidMonotonic` generates ids that increase strictly from one generator, also when several fall in one millisecond and when the clock moves back, following the wire format's monotonic rule; it is built from `UxidOptions`, keeps its state in the instance, and is safe for concurrent use.
- `IUxidDraws` lets a `UxidMonotonic` replay known seeds and steps in place of its byte source, for reproducible vectors such as the conformance corpus's.
- A `UxidMonotonic` refuses `UxidOptions.From` with `UxidErrorCode.FromWithMonotonic` (`from_with_monotonic`) and `UxidOptions.Rand` with `UxidErrorCode.RandWithMonotonic` (`rand_with_monotonic`).
