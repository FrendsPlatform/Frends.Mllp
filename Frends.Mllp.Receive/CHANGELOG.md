# Changelog

## [2.2.0] - 2026-11-06

### Added

- `Result.Warnings` lists non-fatal problems from the run (listener failed to stop, log file could not be created or written, cleanup failed). Empty when there were none. When the task fails and `ThrowErrorOnFailure` is true, the warnings are appended to the exception message.

### Changed

- Better parameter validation
- Improved handling of incoming messages with automatic receive closing and checks on listener status.
- A message that fails processing before it is acknowledged receives a NACK and is not included in the result, so a retry by the sender does not produce a duplicate.
- Stopping the listener no longer uses the process cancellation token, so a cancelled process cannot abort the shutdown.

## [2.1.0] - 2026-08-06

### Changed

- Updated package copyright to comply with Frends task standards.

## [2.0.0] - 2026-06-05

### Added

- `WriteMessagesToFile` and `MessageOutputDirectory` options to handle streaming messages to temp files instead of memory - Output returns file paths. Users are responsible for managing temp files.
- `MaxConcurrentConnections` option to limit simultaneous connections - excess connections are rejected and logged.
- `MaxMessageSize` option to reject oversized messages with a NACK.
- `EnableLogging`, `LogFilePath` and `LogMessageContent` options to log message events to a file.
- `StartBlockByte`, `EndBlockByte`, `CarriageReturnByte` and `CarriageReturnRequired` options to configure MLLP framing.
- `AcknowledgementFormat`, `AckSenderApplication`, `AckReceiverApplication`, `AckHl7Version` and `AcknowledgementType` options to configure ACK/NACK behavior.

### Changed

- ACK generation now uses only the MSH line instead of full payload - fixes crash on large messages.
- [Breaking change] `AcknowledgementMessage` renamed to `AcknowledgementType` in `Connection` and changed from `string` to `AcknowledgementType` enum. Valid values: `AA`, `AE`, `AR`.

## [1.4.0] - 2026-05-18

### Added

- `Options.AcknowledgementFormat` with `Hl7` (default) and `ControlByte` to configure outbound acknowledgement format.
- `Options.AcknowledgementByte` (default `0x06`) for the positive control-byte acknowledgement value.

## [1.3.0] - 2026-05-06

### Added

- `Options.StartBlockByte`, `Options.EndBlockByte`, and `Options.CarriageReturnByte` to allow configuring MLLP framing bytes (defaults: 11, 28, 13)

## [1.2.0] - 2026-04-17

### Added

- `Connection.ClientCertificateThumbprints` property to specify client certificate thumbprints for authentication in
  MTLS mode

## [1.1.0] - 2026-04-13

### Added

- `Connection.Encoding` is now selectable and can be set custom by user

## [1.0.0] - 2026-02-06

### Added

- Initial implementation
