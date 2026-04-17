# Changelog

## [1.3.0] - 2026-04-17

### Added

- `Connection.ServerCertificateThumbprints` to allow pinning the expected server certificate in MTLS mode when `IgnoreServerCertificateErrors` is `false`

## [1.2.0] - 2026-04-13

### Changed

- `Connection.Encoding` is now selectable and can be set custom by user

## [1.1.0] - 2026-03-12

### Added

- `Connection.Encoding` parameter to allow configuring the character encoding used when sending HL7 messages (default:
  `UTF-8`)

## [1.0.0] - 2026-02-05

### Added

- Initial implementation
