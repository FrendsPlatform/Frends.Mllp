using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Receive.Definitions;

/// <summary>
/// Additional parameters.
/// </summary>
public class Options
{
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

    /// <summary>
    /// Encoding used to read incoming HL7 messages.
    /// </summary>
    /// <example>FileEncoding.UTF8</example>
    [DefaultValue(FileEncoding.UTF8)]
    public FileEncoding MessageEncoding { get; set; } = FileEncoding.UTF8;

    /// <summary>
    /// Custom encoding name, used when MessageEncoding is set to Other.
    /// </summary>
    /// <example>iso-8859-1</example>
    [UIHint(nameof(FileEncoding), "", FileEncoding.Other)]
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("")]
    public string EncodingInString { get; set; } = string.Empty;
}
