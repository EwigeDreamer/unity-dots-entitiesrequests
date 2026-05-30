# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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