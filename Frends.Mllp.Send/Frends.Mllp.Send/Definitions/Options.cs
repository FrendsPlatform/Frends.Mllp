using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

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

    /// <summary>
    /// Encoding used to encode the HL7 message before sending.
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

    internal Encoding GetEncoding()
    {
        return MessageEncoding switch
        {
            FileEncoding.UTF8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            FileEncoding.Default => Encoding.Default,
            FileEncoding.ASCII => Encoding.ASCII,
            FileEncoding.Unicode => Encoding.Unicode,
            FileEncoding.Windows1252 => GetExtendedEncoding("windows-1252"),
            FileEncoding.Other => GetExtendedEncoding(EncodingInString),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
    }

    private static Encoding GetExtendedEncoding(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(name);
    }
}
