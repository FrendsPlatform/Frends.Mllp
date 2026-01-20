using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Connection parameters.
/// </summary>
public class Connection
{
    /// <summary>
    /// How long the listener waits for incoming messages before shutting down. Value in seconds.
    /// </summary>
    /// <example>30</example>
    [DefaultValue(30)]
    [Range(1, int.MaxValue)]
    public int ListenDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Size of the buffer used when reading data from clients.
    /// </summary>
    /// <example>8192</example>
    [DefaultValue(8192)]
    [Range(256, int.MaxValue)]
    public int BufferSize { get; set; } = 8192;

    /// <summary>
    /// Encoding used to read messages.
    /// </summary>
    /// <example>UTF-8</example>
    [DefaultValue("UTF-8")]
    public string Encoding { get; set; } = "UTF-8";

    /// <summary>
    /// Whether to send a simple acknowledgement for each message.
    /// </summary>
    /// <example>true</example>
    [DefaultValue(true)]
    public bool SendAcknowledgement { get; set; } = true;

    /// <summary>
    /// Payload of the acknowledgement message. Wrapped in MLLP start/end characters automatically.
    /// </summary>
    /// <example>ACK</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("AA")]
    public string AcknowledgementMessage { get; set; } = "AA";

    internal Encoding GetEncoding() => System.Text.Encoding.GetEncoding(Encoding);
}