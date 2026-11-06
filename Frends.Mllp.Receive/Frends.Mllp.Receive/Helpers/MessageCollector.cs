using System.Collections.Generic;

namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Collects received messages and closes atomically when the result is taken.
/// A message is either added before <see cref="Close"/> and included in its snapshot,
/// or refused after it. The caller must send an ACK only when <see cref="TryAdd"/> returned true,
/// which guarantees that every acknowledged message is part of the result.
/// </summary>
internal sealed class MessageCollector
{
    private readonly object gate = new();
    private readonly List<string> items = new();
    private bool closed;

    /// <summary>
    /// Adds an item unless the collector is closed.
    /// </summary>
    /// <param name="item">Message content or message file path.</param>
    /// <returns>True if the item will be part of the result.</returns>
    internal bool TryAdd(string item)
    {
        lock (gate)
        {
            if (closed)
                return false;

            items.Add(item);
            return true;
        }
    }

    /// <summary>
    /// Closes the collector and returns everything added so far. Safe to call repeatedly.
    /// </summary>
    /// <returns>Snapshot of the added items in arrival order.</returns>
    internal string[] Close()
    {
        lock (gate)
        {
            closed = true;
            return items.ToArray();
        }
    }
}
