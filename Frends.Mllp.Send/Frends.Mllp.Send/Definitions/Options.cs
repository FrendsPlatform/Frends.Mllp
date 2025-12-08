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
