# Changelog

## [1.6.0] - 2026-08-06

### Changed

- Updated copyright notice to comply with Frends standard requirements.

## [1.5.0] - 2026-06-22

### Added

- `Options.MaxMessageSize` to reject outgoing messages exceeding a configurable size limit (0 = unlimited), logged as MESSAGE REJECTED when enabled
- `Options.KeepConnectionAlive` and `Options.ConnectionCacheExpirationMinutes` to reuse a single MLLP connection across multiple Send executions instead of opening a new connection each time
- `Options.RetryCount` and `Options.RetryIntervalSeconds` to automatically retry sending on failure, with a configurable delay between attempts
- ACK parsing and classification (AckResultType: Accept, Error, Reject, Invalid, NotApplicable) based on MSA-1/MSA-3, returned via Result.AckResultType, Result.AckCodeValue, and Result.AckErrorDescription
- `Options.AcceptableAckCodes` to configure which ACK classifications are treated as a successful send (All, Success, Error, Reject)
- `Options.EnableLogging`, `Options.LogFilePath`, and `Options.LogMessageContent` to log message send events (sent, success, failure, rejected, retry, connection dropped, ACK failure) to file

## [1.4.0] - 2026-05-06

### Added

- `Options.StartBlockByte`, `Options.EndBlockByte`, and `Options.CarriageReturnByte` to allow configuring MLLP framing bytes (defaults: 11, 28, 13)

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
