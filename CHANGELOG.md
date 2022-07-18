# Changelog for `FSharp.CosmosDb`

## [1.1.0] - 2022-07-18

### Added

- Can now delete a container, thanks to [#64](https://github.com/aaronpowell/FSharp.CosmosDb/pull/64)
- Can now create a container (to be inline with delete features)

## [1.0.0] - 2022-04-16

**This release has breaking changes from pre-v1**

### Changed

- Moved away from `Azure.Cosmos` 'V4' SDK to use `Microsoft.Azure.Cosmos` 'V3' SDK
  - v4 has no GA date and new features land in v3
- No longer using `AsyncPagable` or `Page<T>` as that was in v4
- `execBatchAsync` now accepts a batch size so it can properly paginate
- Updated analyzer dependencies
- The record representing a connection to Cosmos is now a disposable object
- Upgraded .NET 6

### Added

- New APIs for getting the raw SDK version of the CosmosClient, Database and Container
- New API for working with the Cosmos Change Feed in a F# manner
- Sample showing how to use the Change Feed, works with the existing sample but can be run standalone
- New `Cosmos.dispose` method for disposing of a connection (just wraps the call on `ConnectionOperation` for disposable)

## [0.5.2] - 2021-04-09

### Fixed

- Overhaul of release pipeline

## [0.5.0] - 2021-04-09

### Added

- New `IdAttribute` for marking the ID field of the records
- New API methods for doing `read` and `replace` operations with API
- Analyzer will now detect missing `@` on parameters and provide a fix

### Changed

- Improved the PartitionKey detection logic
- Dependency upgrades across the board

### Fixed

- Parameter handling wasn't very accurate
- `Cosmos.query` now accepts a generic argument

## [0.4.0] - 2020-12-22

### Changed

- Updated to .NET 5
- Support for Ionide 5
- Updated FSAC

## [0.3.0] - 2020-12-21

### Added

- New CI/CD pipeline
- Primitive support for pagination [#23](https://github.com/aaronpowell/FSharp.CosmosDb/issues/23)
- Upsert support [#43](https://github.com/aaronpowell/FSharp.CosmosDb/issues/43)

### Changed

- Upgraded a lot of dependencies
- Analyzer attempts to discover connection information from appsettings.json and appsettings.Development.json

## [0.2.0] - 2020-04-25

### Added

- Ability to create a connection from a connection string with `Cosmos.fromConnectionString`
- Insert API
- Update API
- Delete API

### Changed

- Introduced a `maybe` computational expression to simplify option types
- Major refactor of the internals
- Change analyzer to support using appsettings not just environment variables to find connection info
- Bumped dependency for FSAC to 35.0.0

## [0.1.1] - 2020-03-13

### Changed

- CI/CD pipeline working

## [0.1.0] - 2020-03-12

Initial Release :tada:

### Added

- Basic query API
- Basic Analyzer support
- Tests
