namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Defines the encryption and authentication level for the MLLP connection.
/// </summary>
public enum TlsMode
{
    /// <summary>
    /// No encryption. Data is sent in plain text (standard TCP).
    /// </summary>
    None,

    /// <summary>
    /// Mutual TLS. Both client and server must provide valid certificates to establish a secure, encrypted connection.
    /// </summary>
    Mtls,
}

/// <summary>
/// Specifies the character encoding used for the HL7 message.
/// </summary>
public enum FileEncoding
{
    /// <summary>
    /// UTF-8 encoding.
    /// </summary>
    UTF8,

    /// <summary>
    /// The system default encoding.
    /// </summary>
    Default,

    /// <summary>
    /// ASCII encoding.
    /// </summary>
    ASCII,

    /// <summary>
    /// Unicode (UTF-16) encoding.
    /// </summary>
    Unicode,

    /// <summary>
    /// Windows-1252 encoding.
    /// </summary>
    Windows1252,

    /// <summary>
    /// A custom encoding specified as a string.
    /// </summary>
    Other,
}

/// <summary>
/// Specifies how acknowledgement responses are sent after a successful receive.
/// </summary>
public enum AcknowledgementFormat
{
    /// <summary>
    /// Sends a standard HL7 ACK message wrapped in configured framing bytes.
    /// </summary>
    Hl7,

    /// <summary>
    /// Sends a framed single-byte control acknowledgement.
    /// </summary>
    ControlByte,

    /// <summary>
    /// No automatic ACK is generated. The process flow is responsible for providing
    /// the ACK content. Use this when external logic determines the acknowledgement.
    /// </summary>
    ProcessGenerated,
}
