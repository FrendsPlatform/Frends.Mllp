using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NHapiTools.Base.Net;
using NHapiTools.Base.Util;

internal class MtlsMllpWrapper : IDisposable
{
    private static readonly FieldInfo TcpField = typeof(SimpleMLLPClient).GetField("tcpClient", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(nameof(SimpleMLLPClient), "tcpClient");

    private static readonly FieldInfo StreamToUseField = typeof(SimpleMLLPClient).GetField("streamToUse", BindingFlags.Instance | BindingFlags.NonPublic)
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

    internal void EnableMtls(X509Certificate2 clientCert, string hostname, bool ignoreErrors)
    {
        var tcpClient = (TcpClient)TcpField.GetValue(_client);

        var sslStream = new SslStream(tcpClient.GetStream(), false, (sender, cert, chain, errors) =>
        {
            if (ignoreErrors) return true;
            return errors == SslPolicyErrors.None;
        });

        var certs = new X509Certificate2Collection(clientCert);

        sslStream.AuthenticateAsClient(hostname, certs, SslProtocols.Tls12, false);

        StreamToUseField.SetValue(_client, sslStream);
        _activeStream = sslStream;
    }

    internal string Send(string message, double timeout)
    {
        return _client.SendHL7Message(message, timeout);
    }

    internal void SendOnly(string message)
    {
        string framed = MLLP.CreateMLLPMessage(message);
        var writer = new StreamWriter(_activeStream, _encoding);
        writer.Write(framed);
        writer.Flush();
    }
}