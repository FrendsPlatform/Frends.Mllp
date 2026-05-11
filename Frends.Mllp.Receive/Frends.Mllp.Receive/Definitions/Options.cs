using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

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
    [Range(0, 255, ErrorMessage = "StartBlockByte must be between 0 and 255.")]
    public int StartBlockByte { get; set; } = 11;

    /// <summary>
    /// The MLLP end-block framing byte (decimal). Default is 28 (0x1C, file separator).
    /// </summary>
    /// <example>28</example>
    [DefaultValue(28)]
    [Range(0, 255, ErrorMessage = "EndBlockByte must be between 0 and 255.")]
    public int EndBlockByte { get; set; } = 28;

    /// <summary>
    /// The MLLP end-of-block trailer byte (decimal). Default is 13 (0x0D, carriage return).
    /// </summary>
    /// <example>13</example>
    [DefaultValue(13)]
    [Range(0, 255, ErrorMessage = "CarriageReturnByte must be between 0 and 255.")]
    public int CarriageReturnByte { get; set; } = 13;

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
