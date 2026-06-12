using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Frends.Mllp.Receive.Helpers;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Additional parameters.
/// </summary>
public class Options
{
    /// <summary>
    /// The MLLP start-block framing byte (decimal). Default is 11 (0x0B, vertical tab).
    /// </summary>
    /// <example>11</example>
    [DefaultValue(11)]
    public byte StartBlockByte { get; set; } = 11;

    /// <summary>
    /// The MLLP end-block framing byte (decimal). Default is 28 (0x1C, file separator).
    /// </summary>
    /// <example>28</example>
    [DefaultValue(28)]
    public byte EndBlockByte { get; set; } = 28;

    /// <summary>
    /// The MLLP end-of-block trailer byte (decimal). Default is 13 (0x0D, carriage return).
    /// </summary>
    /// <example>13</example>
    [DefaultValue(13)]
    public byte CarriageReturnByte { get; set; } = 13;

    /// <summary>
    /// Whether the CarriageReturn character is required. If false, EndBlock marks end of message.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool CarriageReturnRequired { get; set; } = true;

    /// <summary>
    /// Maximum number of concurrent connections allowed. 0 means unlimited.
    /// </summary>
    /// <example>10</example>
    [DefaultValue(0)]
    [Range(0, int.MaxValue, ErrorMessage = "MaxConcurrentConnections cannot be negative.")]
    public int MaxConcurrentConnections { get; set; } = 0;

    /// <summary>
    /// Maximum message size in bytes. Messages exceeding this limit will be rejected. 0 means unlimited.
    /// </summary>
    /// <example>1048576</example>
    [DefaultValue(0)]
    [Range(0, int.MaxValue, ErrorMessage = "MaxMessageSize cannot be negative.")]
    public int MaxMessageSize { get; set; } = 0;

    /// <summary>
    /// Format used for outbound acknowledgement responses.
    /// </summary>
    /// <example>AcknowledgementFormat.Hl7</example>
    [DefaultValue(AcknowledgementFormat.Hl7)]
    public AcknowledgementFormat AcknowledgementFormat { get; set; } = AcknowledgementFormat.Hl7;

    /// <summary>
    /// Positive acknowledgement control byte used when AcknowledgementFormat is set to ControlByte.
    /// </summary>
    /// <example>6</example>
    [DefaultValue(6)]
    [UIHint(nameof(AcknowledgementFormat), "", AcknowledgementFormat.ControlByte)]
    public byte AcknowledgementByte { get; set; } = 0x06;

    /// <summary>
    /// Enable message processing logging to file.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(false)]
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// File path for logging message processing events. If not specified, logs to a default location.
    /// </summary>
    /// <example>C:\Logs\mllp-messages.log</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    [RequiredIf(nameof(EnableLogging), true, ErrorMessage = "LogFilePath is required when logging is enabled.")]
    [UIHint(nameof(EnableLogging), "", true)]
    public string LogFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Include full message content in logs. If false, only logs message metadata and status.
    /// </summary>
    /// <example>false</example>
    [DefaultValue(false)]
    [UIHint(nameof(EnableLogging), "", true)]
    public bool LogMessageContent { get; set; } = false;

    /// <summary>
    /// When enabled, messages are written directly to temp files during receive,
    /// reducing memory usage.
    /// Note: uses synchronous file I/O which may impact
    /// throughput under high load. Output contains file paths instead of message content.
    /// Users are responsible for managing and deleting files.
    /// </summary>
    /// <example>false</example>
    [DefaultValue(false)]
    public bool WriteMessagesToFile { get; set; } = false;

    /// <summary>
    /// Directory for temp files when WriteMessagesToFile is enabled.
    /// If empty, uses system temp directory.
    /// </summary>
    /// <example>C:\Temp\mllp</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    [UIHint(nameof(WriteMessagesToFile), "", true)]
    public string MessageOutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Whether to throw an error on failure.
    /// </summary>
    /// <example>false</example>
    [DefaultValue(true)]
    public bool ThrowErrorOnFailure { get; set; } = true;

    /// <summary>
    /// Overrides the error message on failure.
    /// </summary>
    /// <example>Custom error message</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ErrorMessageOnFailure { get; set; } = string.Empty;
}
