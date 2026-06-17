using Frends.Mllp.Send.Helpers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Send.Definitions;

/// <summary>
/// Additional parameters.
/// </summary>
public class Options
{
    /// <summary>
    /// Validate and normalize the HL7 payload using NHapi before sending.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool ValidateWithNhapi { get; set; } = true;

    /// <summary>
    /// Read the acknowledgement from the listener after sending the message.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool ExpectAcknowledgement { get; set; } = true;

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
    /// Maximum message size in bytes. Messages exceeding this limit will not be sent. 0 means unlimited.
    /// </summary>
    /// <example>1048576</example>
    [DefaultValue(0)]
    [Range(0, int.MaxValue, ErrorMessage = "MaxMessageSize cannot be negative.")]
    public int MaxMessageSize { get; set; } = 0;

    /// <summary>
    /// Keep the TCP connection alive and reuse it across multiple executions.
    /// When enabled, the connection is cached for the duration of the sliding expiration window.
    /// </summary>
    /// <example>false</example>
    [DefaultValue(false)]
    public bool KeepConnectionAlive { get; set; } = false;

    /// <summary>
    /// How long (in minutes) an idle cached connection is kept alive before being closed.
    /// Only used when KeepConnectionAlive is true.
    /// </summary>
    /// <example>5</example>
    [DefaultValue(5)]
    [UIHint(nameof(KeepConnectionAlive), "", true)]
    [Range(1, 60, ErrorMessage = "ConnectionCacheExpirationMinutes must be between 1 and 60.")]
    public int ConnectionCacheExpirationMinutes { get; set; } = 5;

    /// <summary>
    /// Enable message processing logging to file.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(false)]
    public bool EnableLogging { get; set; } = false;

    /// <summary>
    /// File path for logging message send events. If not specified, logs to a default location.
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
