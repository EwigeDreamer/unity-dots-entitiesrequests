# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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