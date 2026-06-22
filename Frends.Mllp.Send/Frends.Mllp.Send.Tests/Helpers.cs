using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Frends.Mllp.Send.Tests;

internal static class Helpers
{
    internal static Task<string> ReadMllpMessage(
        Stream stream,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        return ReadMllpMessage(stream, encoding, cancellationToken, 0x0b, 0x1c);
    }

    internal static async Task<string> ReadMllpMessage(
        Stream stream,
        Encoding encoding,
        CancellationToken cancellationToken,
        byte startBlock,
        byte endBlock)
    {
        using var messageBuffer = new MemoryStream();
        var buffer = new byte[1];
        var started = false;

        while (await stream.ReadAsync(buffer, 0, 1, cancellationToken) > 0)
        {
            var current = buffer[0];

            if (current == startBlock)
            {
                started = true;

                continue;
            }

            if (!started) continue;

            if (current == endBlock)
            {
                return encoding.GetString(messageBuffer.ToArray());
            }

            messageBuffer.WriteByte(current);
        }

        throw new Exception("Connection closed before MLLP message was fully received.");
    }

    internal static string BuildTestMessage(string patientName = "Doe^John") =>
        "MSH|^~\\&|SendingApp|SendingFac|ReceivingApp|ReceivingFac|20250101010101||ADT^A01|MSG00001|P|2.5.1\r" +
        "EVN|A01|20250101010101\r" +
        $"PID|1||12345^^^Hospital^MR||{patientName}||19800101|M";

    internal static string BuildAck(string controlId) =>
        $"MSH|^~\\&|Listener|ListenerFacility|Sender|SenderFacility|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK^A01|ACK0001|P|2.5.1\r" +
        $"MSA|AA|{controlId}\r";

    internal static string ExtractControlId(string hl7Message)
    {
        var segments = hl7Message.Split('\r', StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (!segment.StartsWith("MSH|", StringComparison.Ordinal))
                continue;

            var fields = segment.Split('|');

            if (fields.Length > 9)
                return fields[9];
        }

        return string.Empty;
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

    internal static string BuildLargeTestMessage(int sizeInMb)
    {
        var targetBytes = sizeInMb * 1024 * 1024;

        var filler = new string('A', targetBytes);

        return "MSH|^~\\&|TestSystem|TestFacility|ReceivingApp|ReceivingFacility|" +
               $"{DateTime.UtcNow:yyyyMMddHHmmss}||ADT^A01|MSG00001|P|2.5.1\r" +
               "PID|1||12345^^^Hospital^MR||Doe^John||19800101|M\r" +
               $"OBX|1|TX|DOC^Document||{filler}||||||F\r";
    }
}
