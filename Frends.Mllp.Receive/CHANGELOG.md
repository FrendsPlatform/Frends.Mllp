# Changelog

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
