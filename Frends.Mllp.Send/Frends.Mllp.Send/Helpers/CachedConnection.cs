using System;
using System.Threading;

namespace Frends.Mllp.Send.Helpers;

internal sealed class CachedConnection : IDisposable
{
    public CachedConnection(MtlsMllpWrapper wrapper)
    {
        Wrapper = wrapper;
    }

#pragma warning disable FT0014 // Documentation required tags are missing
    internal MtlsMllpWrapper Wrapper { get; }

    internal SemaphoreSlim Lock { get; } = new(1, 1);

#pragma warning restore FT0014

    /// <summary>
    /// Disposes the cached connection, releasing the lock and the wrapper.
    /// </summary>
    public void Dispose()
    {
        Lock.Dispose();
        Wrapper.Dispose();
    }
}
