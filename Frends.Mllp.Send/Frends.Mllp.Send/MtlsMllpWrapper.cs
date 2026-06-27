using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NHapiTools.Base.Net;

namespace Frends.Mllp.Send;

internal class MtlsMllpWrapper : IDisposable
{
    private static readonly FieldInfo TcpField =
        typeof(SimpleMLLPClient).GetField("tcpClient", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(SimpleMLLPClient), "tcpClient");

    private static readonly FieldInfo StreamToUseField =
        typeof(SimpleMLLPClient).GetField("streamToUse", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(SimpleMLLPClient), "streamToUse");

    private readonly SimpleMLLPClient _client;
    private readonly Encoding _encoding;
    private Stream _activeStream;

    public MtlsMllpWrapper(string host, int port, Encoding encoding, int timeoutMs)
    {
        _encoding = encoding ?? Encoding.ASCII;
        _client = new SimpleMLLPClient(host, port, _encoding, timeoutMs);
        _activeStream = (Stream)StreamToUseField.GetValue(_client);
    }

#pragma warning disable FT0014 // Documentation required tags are missing
    public void Dispose() => _client?.Dispose();
#pragma warning restore FT0014 // Documentation required tags are missing

    internal void EnableMtls(
        X509Certificate2 clientCert,
        string hostname,
        bool ignoreErrors,
        string[] serverCertificateThumbprints)
    {
        var tcpClient = (TcpClient)TcpField.GetValue(_client);

        var sslStream = new SslStream(tcpClient.GetStream(), false, (_, cert, _, errors) =>
        {
            if (ignoreErrors) return true;

            if (serverCertificateThumbprints.Length <= 0)
                return errors == SslPolicyErrors.None;

            if (cert is null) return false;
            var thumbprint = Normalize(cert.GetCertHashString());

            return errors != SslPolicyErrors.RemoteCertificateNotAvailable && Array.Exists(
                serverCertificateThumbprints,
                expected => !string.IsNullOrWhiteSpace(expected) &&
                            Normalize(expected).Equals(thumbprint, StringComparison.OrdinalIgnoreCase));
        });

        var certs = new X509Certificate2Collection(clientCert);

        sslStream.AuthenticateAsClient(hostname, certs, SslProtocols.Tls12, false);

        StreamToUseField.SetValue(_client, sslStream);
        _activeStream = sslStream;
    }

    internal string Send(string message, double timeoutMs, byte startBlock, byte endBlock, byte carriageReturn)
    {
        WriteFramed(message, startBlock, endBlock, carriageReturn);

        if (_activeStream.CanTimeout)
            _activeStream.ReadTimeout = (int)timeoutMs;

        using var responseBuffer = new MemoryStream();
        var started = false;
        var pendingEndBlock = false;

        while (true)
        {
            int currentValue;
            try
            {
                currentValue = _activeStream.ReadByte();
            }
            catch (IOException ex) when (IsReadTimeout(ex))
            {
                throw new TimeoutException($"Reading the HL7 reply timed out after {(int)timeoutMs} milliseconds.");
            }

            if (currentValue == -1)
                throw new IOException("Connection closed before HL7 reply was fully received.");

            var current = (byte)currentValue;

            if (!started)
            {
                if (current == startBlock)
                    started = true;

                continue;
            }

            if (pendingEndBlock)
            {
                if (current == carriageReturn)
                    break;

                responseBuffer.WriteByte(endBlock);
                pendingEndBlock = false;
            }

            if (current == endBlock)
            {
                pendingEndBlock = true;
                continue;
            }

            responseBuffer.WriteByte(current);
        }

        return _encoding.GetString(responseBuffer.ToArray());
    }

    internal void SendOnly(string message, byte startBlock, byte endBlock, byte carriageReturn)
    {
        WriteFramed(message, startBlock, endBlock, carriageReturn);
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Replace(":", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    private static bool IsReadTimeout(IOException exception)
    {
        if (exception.InnerException is SocketException socketException
            && socketException.SocketErrorCode == SocketError.TimedOut)
        {
            return true;
        }

        return exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase);
    }

    private void WriteFramed(string message, byte startBlock, byte endBlock, byte carriageReturn)
    {
        var payloadBytes = _encoding.GetBytes(message);
        var framed = new byte[payloadBytes.Length + 3];

        framed[0] = startBlock;
        Buffer.BlockCopy(payloadBytes, 0, framed, 1, payloadBytes.Length);
        framed[^2] = endBlock;
        framed[^1] = carriageReturn;

        _activeStream.Write(framed, 0, framed.Length);
        _activeStream.Flush();
    }
}
