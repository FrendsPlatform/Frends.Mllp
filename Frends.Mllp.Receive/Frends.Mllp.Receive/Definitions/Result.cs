namespace Frends.Mllp.Receive.Definitions;

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
    /// Messages received while the listener was running.
    /// </summary>
    /// <example>ACK</example>
    public string[] Output { get; set; }

    /// <summary>
    /// Non-fatal problems that occurred during the run, for example a listener that failed to stop,
    /// a log file that could not be written, or a cleanup step that failed. Empty if there were none.
    /// </summary>
    /// <example>["Failed to stop the MLLP host cleanly: ..."]</example>
    public string[] Warnings { get; set; } = [];

    /// <summary>
    /// Error that occurred during task execution.
    /// </summary>
    /// <example>object { string Message, Exception AdditionalInfo }</example>
    public Error Error { get; set; }
}
