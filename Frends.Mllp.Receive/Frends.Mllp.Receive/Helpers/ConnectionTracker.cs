using System.Threading;

namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Tracks active connections to enforce connection limits.
/// </summary>
internal sealed class ConnectionTracker
{
    private readonly int maxConnections;
    private int activeConnections;

    public ConnectionTracker(int maxConnections)
    {
        this.maxConnections = maxConnections;
        activeConnections = 0;
    }

    internal int ActiveConnections => activeConnections;

    internal bool TryIncrementConnection()
    {
        if (maxConnections <= 0)
            return true;

        var current = Interlocked.Increment(ref activeConnections);

        if (current > maxConnections)
        {
            Interlocked.Decrement(ref activeConnections);
            return false;
        }

        return true;
    }

    internal void DecrementConnection()
    {
        if (maxConnections > 0)
            Interlocked.Decrement(ref activeConnections);
    }
}