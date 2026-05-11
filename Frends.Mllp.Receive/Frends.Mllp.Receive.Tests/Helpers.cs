using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Frends.Mllp.Receive.Tests;

public static class Helpers
{
    internal static Task<string> SendMessageAsync(
        int port,
        string message,
        string clientCertPath = null,
        string password = null,
        Encoding encoding = null)
    {
        return SendMessageAsync(port, message, 0x0b, 0x1c, 0x0d, clientCertPath, password, encoding);
    }

    internal static async Task<string> SendMessageAsync(
        int port,
        string message,
        byte startBlock,
        byte endBlock,
        byte carriageReturn,
        string clientCertPath = null,
        string password = null,
        Encoding encoding = null)
    {
        using var client = new TcpClient();

        for (int i = 0; i < 10; i++)
        {
            try
            {
                await client.ConnectAsync(
                    IPAddress.Loopback,
                    port);

                break;
            }
            catch (SocketException)
            {
                if (i == 9) throw;
                await Task.Delay(500);
            }
        }

        SslStream sslStream = null;

        try
        {
            Stream currentStream = client.GetStream();

            if (!string.IsNullOrEmpty(clientCertPath))
            {
                sslStream = new SslStream(
                    currentStream,
                    true);

                using var clientCert = new X509Certificate2(
                    clientCertPath,
                    password);
                var clientCerts = new X509Certificate2Collection(clientCert);

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ClientCertificates = clientCerts,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                };

                await sslStream.AuthenticateAsClientAsync(
                    sslOptions,
                    CancellationToken.None);
                currentStream = sslStream;
            }

            var sendEncoding = encoding ?? Encoding.UTF8;
            var messageBytes = sendEncoding.GetBytes(message);
            var bytes = new byte[messageBytes.Length + 3];
            bytes[0] = startBlock;
            Buffer.BlockCopy(messageBytes, 0, bytes, 1, messageBytes.Length);
            bytes[^2] = endBlock;
            bytes[^1] = carriageReturn;

            await currentStream.WriteAsync(
                bytes,
                0,
                bytes.Length);
            await currentStream.FlushAsync();

            var buffer = new byte[8192];
            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                var read = await currentStream.ReadAsync(
                    buffer,
                    0,
                    buffer.Length,
                    readCts.Token);

                if (read <= 0) return string.Empty;

                var ackPayload = Encoding.UTF8.GetString(
                    buffer,
                    0,
                    read);

                return StripMllpFrame(ackPayload);
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
        }
        finally
        {
            if (sslStream != null) await sslStream.DisposeAsync();
        }
    }

    internal static string StripMllpFrame(string framed)
    {
        if (string.IsNullOrEmpty(framed))
            return framed;

        var trimmed = framed;
        if (trimmed[0] == '\u000b')
            trimmed = trimmed[1..];
        if (trimmed.EndsWith(
                "\u001c\r",
                StringComparison.Ordinal))
            trimmed = trimmed[..^2];

        return trimmed;
    }

    internal static int GetAvailablePort()
    {
        var listener = new TcpListener(
            IPAddress.Loopback,
            0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }

    internal static CertificateStoreCleanup TrustCertificateForCurrentUserRoot(string pfxPath, string password)
    {
        using var certificateWithPrivateKey = new X509Certificate2(pfxPath, password);
        using var certificate = new X509Certificate2(certificateWithPrivateKey.Export(X509ContentType.Cert));
        using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadWrite);

        var existing = store.Certificates.Find(
            X509FindType.FindByThumbprint,
            certificate.Thumbprint,
            validOnly: false);

        if (existing.Count > 0) return null;

        store.Add(certificate);

        return new CertificateStoreCleanup(StoreName.Root, StoreLocation.CurrentUser, certificate.Thumbprint);
    }
}
