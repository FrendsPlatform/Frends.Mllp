using System.Collections.Generic;

namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Collects non-fatal problems during a run so they can be returned in the result or error,
/// which is the only channel visible in Frends.
/// </summary>
internal sealed class Diagnostics
{
    private const int MaxWarnings = 20;
    private readonly object gate = new();
    private readonly List<string> warnings = new();

    /// <summary>
    /// Records a warning. At most <see cref="MaxWarnings"/> are kept, followed by a single suppression notice.
    /// </summary>
    /// <param name="message">What went wrong.</param>
    internal void Warn(string message)
    {
        lock (gate)
        {
            if (warnings.Count < MaxWarnings)
                warnings.Add(message);
            else if (warnings.Count == MaxWarnings)
                warnings.Add("Further warnings were suppressed.");
        }
    }

    /// <summary>
    /// Returns the warnings recorded so far.
    /// </summary>
    /// <returns>A snapshot of the warnings.</returns>
    internal string[] ToArray()
    {
        lock (gate)
        {
            return warnings.ToArray();
        }
    }
}
