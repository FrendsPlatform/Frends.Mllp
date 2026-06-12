using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Frends.Mllp.Receive.Helpers;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Connection parameters.
/// </summary>
public class Connection
{
    /// <summary>
    /// The TLS encryption mode to use for the connection.
    /// </summary>
    /// <example>TlsMode.None</example>
    [DefaultValue(TlsMode.None)]
    public TlsMode TlsMode { get; set; } = TlsMode.None;

    /// <summary>
    /// Path to the server certificate file (PFX or P12 format).
    /// Required only for MTLS mode.
    /// </summary>
    /// <example>C:\certs\client.pfx</example>
    [DisplayFormat(DataFormatString = "Text")]
    public string ServerCertPath { get; set; }

    /// <summary>
    /// Password for the server certificate.
    /// </summary>
    /// <example>MyStrongPassword123</example>
    [PasswordPropertyText]
    [DisplayFormat(DataFormatString = "Text")]
    public string ServerCertPassword { get; set; }

    /// <summary>
    /// How long the listener waits for incoming messages before shutting down. Value in seconds.
    /// </summary>
    /// <example>30</example>
    [DefaultValue(30)]
    [Range(1, int.MaxValue, ErrorMessage = "Listen duration must be greater than zero.")]
    public int ListenDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Size of the buffer used when reading data from clients.
    /// </summary>
    /// <example>8192</example>
    [DefaultValue(8192)]
    [Range(1, int.MaxValue, ErrorMessage = "Buffer size must be positive.")]
    public int BufferSize { get; set; } = 8192;

    /// <summary>
    /// Whether to send a simple acknowledgement for each message.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool SendAcknowledgement { get; set; } = true;

    /// <summary>
    /// ACK type to use. AA = Application Accept, AE = Application Error, AR = Application Reject.
    /// </summary>
    /// <example>AA</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue(AcknowledgementType.AA)]
    public AcknowledgementType AcknowledgementType { get; set; } = AcknowledgementType.AA;

    /// <summary>
    /// Sender application name (MSH-3) used in the generated ACK message.
    /// If empty, uses the receiving application (MSH-5) from the incoming message.
    /// </summary>
    /// <example>ACK_APP</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string AckSenderApplication { get; set; } = string.Empty;

    /// <summary>
    /// Receiver application name (MSH-5) used in the generated ACK message.
    /// If empty, uses the sending application (MSH-3) from the incoming message.
    /// </summary>
    /// <example>SENDING_APP</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string AckReceiverApplication { get; set; } = string.Empty;

    /// <summary>
    /// HL7 version used in the generated ACK message (MSH-12).
    /// If empty, uses the version from the incoming message.
    /// </summary>
    /// <example>2.5</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string AckHl7Version { get; set; } = string.Empty;

    /// <summary>
    /// If enabled, the server will accept client certificates even if they are
    /// self-signed or have validation errors (Mutual TLS only).
    /// </summary>
    /// <example>false</example>
    [DefaultValue(false)]
    public bool IgnoreClientCertificateErrors { get; set; } = false;

    /// <summary>
    /// Expected client certificate thumbprint(s) for validation.
    /// Only used when IgnoreClientCertificateErrors is false.
    /// Used in MTLS mode for certificate pinning.
    /// </summary>
    /// <example>E5FA62B8B5F3B0B2B3B4B5B6B7B8B9B0B1B2B3B</example>
    [DisplayFormat(DataFormatString = "Text")]
    [UIHint(nameof(TlsMode), "", TlsMode.Mtls)]
    public string[] ClientCertificateThumbprints { get; set; } = [];

    /// <summary>
    /// Encoding used to read incoming HL7 messages.
    /// </summary>
    /// <example>FileEncoding.UTF8</example>
    [DefaultValue(FileEncoding.UTF8)]
    public FileEncoding Encoding { get; set; } = FileEncoding.UTF8;

    /// <summary>
    /// Custom encoding name, used when Encoding is set to Other.
    /// </summary>
    /// <example>iso-8859-1</example>
    [UIHint(nameof(FileEncoding), "", FileEncoding.Other)]
    [RequiredIf(nameof(Encoding), FileEncoding.Other, ErrorMessage = "EncodingInString must not be empty when Encoding is set to Other.")]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string EncodingInString { get; set; } = string.Empty;
}
