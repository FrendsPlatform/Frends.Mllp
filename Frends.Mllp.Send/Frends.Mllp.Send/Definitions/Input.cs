using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Send.Definitions;

/// <summary>
/// HL7 message payload.
/// </summary>
public class Input
{
    /// <summary>
    /// HL7 message to send over MLLP.
    /// </summary>
    /// <example>MSH|^~\\&amp;|SendingApp|SendingFac|ReceivingApp|ReceivingFac|20250101010101||ADT^A01|MSG00001|P|2.5.1\rEVN|A01|20250101010101\rPID|1||12345^^^Hospital^MR||Doe^John||19800101|M</example>
    [DisplayFormat(DataFormatString = "Text")]
    [DefaultValue("MSH|^~\\&amp;|SendingApp|SendingFac|ReceivingApp|ReceivingFac|20250101010101||ADT^A01|MSG00001|P|2.5.1\\rEVN|A01|20250101010101\\rPID|1||12345^^^Hospital^MR||Doe^John||19800101|M")]
    public string Hl7Message { get; set; }
}
