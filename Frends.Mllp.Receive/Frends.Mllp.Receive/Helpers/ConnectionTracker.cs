namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Tracks active connections to enforce connection limits.
/// </summary>
internal sealed class ConnectionTracker
{
    private readonly object syncLock = new();
    private readonly int maxConnections;
    private int activeConnections;

    public ConnectionTracker(int maxConnections)
    {
        this.maxConnections = maxConnections;
    }

    internal bool TryIncrementConnection()
    {
        if (maxConnections <= 0)
            return true;

        lock (syncLock)
        {
            if (activeConnections >= maxConnections)
                return false;

            activeConnections++;
            return true;
        }
    }

    internal void DecrementConnection()
    {
        if (maxConnections <= 0)
            return;

        lock (syncLock)
        {
            if (activeConnections > 0)
                activeConnections--;
        }
    }
}