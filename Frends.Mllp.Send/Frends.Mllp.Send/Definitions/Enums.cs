namespace Frends.Mllp.Send.Definitions
{
    /// <summary>
    /// Defines the encryption and authentication level for the MLLP connection.
    /// </summary>
    public enum TlsMode
    {
        /// <summary>
        /// No encryption. Data is sent in plain text (standard TCP).
        /// </summary>
        None,

        /// <summary>
        /// Mutual TLS. Both client and server must provide valid certificates to establish a secure, encrypted connection.
        /// </summary>
        Mtls,
    }

    /// <summary>
    /// Specifies the character encoding used for the HL7 message.
    /// </summary>
    public enum FileEncoding
    {
        /// <summary>
        /// UTF-8 encoding.
        /// </summary>
        UTF8,

        /// <summary>
        /// The system default encoding.
        /// </summary>
        Default,

        /// <summary>
        /// ASCII encoding.
        /// </summary>
        ASCII,

        /// <summary>
        /// Unicode (UTF-16) encoding.
        /// </summary>
        Unicode,

        /// <summary>
        /// Windows-1252 encoding.
        /// </summary>
        Windows1252,

        /// <summary>
        /// A custom encoding specified as a string.
        /// </summary>
        Other,
    }

    /// <summary>
    /// Classification of the received HL7 acknowledgement.
    /// </summary>
    public enum AckResultType
    {
        /// <summary>
        /// Acknowledgement code was AA or CA (Application/Commit Accept).
        /// </summary>
        Accept,

        /// <summary>
        /// Acknowledgement code was AE or CE (Application/Commit Error).
        /// </summary>
        Error,

        /// <summary>
        /// Acknowledgement code was AR or CR (Application/Commit Reject).
        /// </summary>
        Reject,

        /// <summary>
        /// Acknowledgement could not be parsed, or MSA-1 contained an unrecognized code.
        /// </summary>
        Invalid,

        /// <summary>
        /// No acknowledgement was expected or received (one-way send).
        /// </summary>
        NotApplicable,
    }

    /// <summary>
    /// Determines which ACK classifications are treated as a successful send.
    /// </summary>
    public enum AcceptableAckCodes
    {
        /// <summary>
        /// AA, CA, AE, CE, AR, CR are all treated as success.
        /// </summary>
        All,

        /// <summary>
        /// Only AA, CA are treated as success.
        /// </summary>
        Success,

        /// <summary>
        /// AA, CA, AE, CE are treated as success.
        /// </summary>
        Error,

        /// <summary>
        /// AA, CA, AR, CR are treated as success.
        /// </summary>
        Reject,
    }
}
