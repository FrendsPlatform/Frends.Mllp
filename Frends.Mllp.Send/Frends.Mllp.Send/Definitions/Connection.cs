using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Send.Definitions;

/// <summary>
/// Connection parameters for the remote MLLP endpoint.
/// </summary>
public class Connection
{
    /// <summary>
    /// Hostname or IP address of the MLLP listener.
    /// </summary>
    /// <example>127.0.0.1</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("127.0.0.1")]
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// TCP port of the MLLP listener.
    /// </summary>
    /// <example>2575</example>
    [DefaultValue(2575)]
    public int Port { get; set; } = 2575;

    /// <summary>
    /// Connection timeout in seconds when opening the socket.
    /// </summary>
    /// <example>30</example>
    [DefaultValue(30)]
    public int ConnectTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Read timeout in seconds when waiting for an acknowledgement.
    /// </summary>
    /// <example>30</example>
    [DefaultValue(30)]
    public int ReadTimeoutSeconds { get; set; } = 30;
}
