# Changelog for `FSharp.CosmosDb`

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
