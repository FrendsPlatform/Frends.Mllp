using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;

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
        CodePagesEncodingProviderRegistrar.EnsureRegistered();
        return Encoding.GetEncoding(name);
    }

    private static class CodePagesEncodingProviderRegistrar
    {
        private static readonly object Lock = new();
        private static bool registered;

        internal static void EnsureRegistered()
        {
            if (registered)
                return;

            lock (Lock)
            {
                if (!registered)
                {
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    registered = true;
                }
            }
        }
    }
}