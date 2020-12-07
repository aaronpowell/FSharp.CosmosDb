# Changelog for `FSharp.CosmosDb`

## [0.3.0]

### Added

- New CI/CD pipeline
- Primitive support for pagination [#23](https://github.com/aaronpowell/FSharp.CosmosDb/issues/23)

## Changed

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
