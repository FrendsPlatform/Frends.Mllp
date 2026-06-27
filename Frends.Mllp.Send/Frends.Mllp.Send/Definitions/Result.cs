namespace Frends.Mllp.Send.Definitions;

/// <summary>
/// Result of the task.
/// </summary>
public class Result
{
    /// <summary>
    /// Indicates if the task completed successfully.
    /// </summary>
    /// <example>true</example>
    public bool Success { get; set; }

    /// <summary>
    /// Acknowledgement returned by the MLLP listener.
    /// </summary>
    /// <example>MSH|^~\&amp;|...</example>
    public string Output { get; set; }

    /// <summary>
    /// Classification of the received acknowledgement (Accept, Error, Reject, Invalid, or NotApplicable for one-way sends).
    /// </summary>
    /// <example>AckResultType.Accept</example>
    public AckResultType AckResultType { get; set; }

    /// <summary>
    /// The raw acknowledgement code extracted from MSA-1 (e.g. AA, AE, AR). Null if no ACK was received or parsing failed.
    /// </summary>
    /// <example>AA</example>
    public string AckCodeValue { get; set; }

    /// <summary>
    /// Error or status description extracted from MSA-3, present on negative acknowledgements.
    /// </summary>
    /// <example>Required field PID-3 is missing</example>
    public string AckErrorDescription { get; set; }

    /// <summary>
    /// Error that occurred during task execution.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
