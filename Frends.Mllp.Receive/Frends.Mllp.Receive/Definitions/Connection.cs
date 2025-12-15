using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Connection parameters.
/// </summary>
public class Connection
{
    /// <summary>
    /// IP address or hostname to bind to. Leave empty to listen on all interfaces.
    /// </summary>
    /// <example>127.0.0.1</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string ListenAddress { get; set; } = string.Empty;
}
