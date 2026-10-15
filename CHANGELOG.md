# Changelog

Changes to the SDK and test tool are recorded here. Package builds alone do not constitute a published release.

## Unreleased

### Changed

- Collection/payment declarations use original CDAR 212 document uploads; removed the JSON payment operation.
- Added lifecycle-event reads and aligned the bundled Client API contract, console guidance and HTTP journey.

### Added

- .NET 10 SDK with 26 named operations, OAuth token caching, validation, bounded uploads and streamed downloads.
- Server-rendered test console with Sandbox/Production configuration, backend-only API access and session handling.
- Local HTTP feature journey and browser fixture mode.
- MIT licensing, usage and contribution guides, and development scripts.

- Multi-stage non-root Docker image and GitHub Actions publishing to GHCR.
- Docker Compose and Kubernetes deployment examples with runtime environment configuration.

- Uppercase `DOCNAUT_` integration environment variables shared by the console, Compose and Kubernetes.

The current package build version is 0.1.0; no public release date is declared here.
