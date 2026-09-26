# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.0.0] - 2026-09-26

### Changed
- **Rewritten from scratch** around a bank/card ownership model: the whole public API changed, see the README.
- `RequestWriter<T>` / `RequestReader<T>` are cards taken from a bank, and **both** must be disposed by the caller — in v1 only the writer was disposable. The old `Requests<T>` container (`GetWriter` / `GetReader` / `Update`) is gone.
- The owner of each request type is a generated Burst system that drains pending writes at the end of the simulation phase, so a reader sees the previous tick.
- Writer cards declare **read** access to the bank marker instead of write, so writer jobs run in parallel rather than serializing against each other; the owner's write declaration still orders the merge after all of them.

### Removed
- The v1 runtime, generator and test suite.

### Fixed
- Generated file names are unique per request type (hash of the full type name): two types may share a short name in different namespaces.

## [1.1.2] - 2026-09-20

### Changed
- `Requests<T>`, `RequestsData<T>`, `NativeRequestBuffer<T>` and `RequestWriter<T>` now accept `AllocatorManager.AllocatorHandle`, supporting custom allocators while remaining source-compatible with the `Allocator` enum.

### Fixed
- Fixed a use-after-free crash during world teardown: the shared request block is now freed only after the owner and all registered writers have been disposed, so either destruction order is safe.

## [1.1.1] - 2026-05-30

### Added
- `RequestReader<T>` now includes `AtomicSafetyHandle`, enabling its safe use inside `IJob`.

## [1.1.0] - 2026-04-29

### Added
- Each `RequestWriter` now owns a dedicated private buffer, eliminating write conflicts between systems.
- `RequestWriter<T>.Dispose()` to free the buffer; `GetRequestWriter` now requires an explicit `initialCapacity`.

### Changed
- Internal redesign: multiple writer buffers aggregated into a single read buffer, removing the need for `EnsureRequestBufferCapacity`.
- Mixed synchronous and parallel writes are now fully supported (each system writes to its own buffer).

### Removed
- `EnsureRequestBufferCapacity` extension methods (obsolete).
- Previous limitation that caused exceptions when mixing write modes.

## [1.0.1] - 2026-04-28

### Added
- Added `ParallelWriteRaceConditionTest` to verify concurrent write behavior and document limitations of mixing sync/parallel writes.

### Changed
- Improved documentation: added a warning about mixing synchronous and parallel writes with a collapsible note.

## [1.0.0] - 2026-04-23

### Added
- Initial stable release of Entities Requests for Unity DOTS.
- Core `Requests<T>` container with double‑buffered `NativeRequestBuffer<T>`.
- `RequestWriter<T>` / `RequestReader<T>` for many‑to‑one command passing.
- Thread‑safe parallel writes via `RequestWriter<T>.ParallelWriter`.
- Source generator for `[assembly: RegisterRequest(typeof(T))]`.
- Extension methods `GetRequestWriter`, `GetRequestReader`, `EnsureRequestBufferCapacity`.
- Full test suite (core, ECS integration, parallel, data integrity) and three usage samples.
- Documentation with performance notes and comparison to Entities Events.

### Changed
- Rewritten to match `EntitiesEvents` architecture; fixed Burst error by using direct field access in `RequestWriter` constructor.
- Renamed capacity method to `EnsureRequestBufferCapacity` to avoid naming conflicts.

### Removed
- Standalone `RequestParallelWriter` (merged into `RequestWriter<T>.ParallelWriter`).
- Redundant `UnsafeRequests` layer.

## [0.1.0] - 2026-04-15
### Added
- .NET project for source generator dll
- Entities requests logic