using System;
using System.Security.Cryptography.X509Certificates;

namespace Frends.Mllp.Send.Tests;

public sealed class CertificateStoreCleanup(StoreName storeName, StoreLocation storeLocation, string thumbprint)
    : IDisposable
{
    private bool disposed;

    public void Dispose()
    {
        if (disposed || string.IsNullOrWhiteSpace(thumbprint))
        {
            return;
        }

        using var store = new X509Store(storeName, storeLocation);
        store.Open(OpenFlags.ReadWrite);

        var certificates = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            thumbprint,
            validOnly: false);

        foreach (var certificate in certificates)
        {
            store.Remove(certificate);
        }

        disposed = true;
    }
}
