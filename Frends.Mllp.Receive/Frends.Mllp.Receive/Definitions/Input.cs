using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Parameters for configuring the local MLLP listener.
/// </summary>
public class Input
{
    /// <summary>
    /// IP address or hostname to bind to. Leave empty to listen on all interfaces.
    /// </summary>
    /// <example>127.0.0.1</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ListenAddress { get; set; } = string.Empty;

    /// <summary>
    /// TCP port the server listens on.
    /// </summary>
    /// <example>2575</example>
    [DefaultValue(2575)]
    [Range(1, 65535)]
    public int Port { get; set; } = 2575;
}