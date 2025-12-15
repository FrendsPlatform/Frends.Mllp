using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Parameters for configuring the local MLLP listener.
/// </summary>
public class Input
{
    /// <summary>
    /// TCP port the server listens on.
    /// </summary>
    /// <example>2575</example>
    [DefaultValue(2575)]
    [Range(1, 65535)]
    public int Port { get; set; } = 2575;
}
