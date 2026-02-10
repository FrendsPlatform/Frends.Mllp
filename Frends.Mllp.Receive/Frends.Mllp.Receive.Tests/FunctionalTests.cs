using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NHapi.Base.Parser;
using NHapi.Base.Util;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class FunctionalTests
{
    private string _clientPfxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/client.pfx");
    private string _serverPfxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/server.pfx");
    private string _password = "password";

    [Test]
    public void ShouldReceiveSingleMessageWithinListenWindow()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection { ListenDurationSeconds = 2, BufferSize = 1024 };
        var options = new Options { };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await SendMessageAsync(port, "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);
        sender.Wait();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(result.Output.First(), Does.Contain("MSH|^~\\&|HIS|RIH"));
    }

    [Test]
    public void ShouldReceiveMultipleMessagesFromMultipleClients()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection { ListenDurationSeconds = 3, BufferSize = 1024 };
        var options = new Options { };

        var sender1 = Task.Run(async () =>
        {
            await Task.Delay(50);
            await SendMessageAsync(port, "MSH|^~\\&|HIS|RIH|EKG|EKG|ONE|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var sender2 = Task.Run(async () =>
        {
            await Task.Delay(150);
            await SendMessageAsync(port, "MSH|^~\\&|HIS|RIH|EKG|EKG|TWO|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);
        Task.WaitAll(sender1, sender2);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.EquivalentTo(new[] { "MSH|^~\\&|HIS|RIH|EKG|EKG|ONE|SECURITY|ADT^A01|MSG00001|P|2.5", "MSH|^~\\&|HIS|RIH|EKG|EKG|TWO|SECURITY|ADT^A01|MSG00001|P|2.5" }));
    }

    [Test]
    public void ShouldReturnEmptyWhenNoMessagesArrive()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection { ListenDurationSeconds = 1 };
        var options = new Options { };

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public void ShouldSendProperAck()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection { ListenDurationSeconds = 2, BufferSize = 1024 };
        var options = new Options { };

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(50);
            return await SendMessageAsync(port, "MSH|^~\\&|SNDAPP|SNDFAC|RCVAPP|RCVFAC|20250101010101||ORM^O01|CTRL123|P|2.5");
        });

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);
        var ack = ackTask.Result;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));

        Assert.That(ack, Is.Not.Null.And.Not.Empty);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(terser.Get("/MSH-9-1"), Is.EqualTo("ACK"));
        Assert.That(terser.Get("/MSH-9-2"), Is.EqualTo("O01"));
        Assert.That(terser.Get("/MSA-1"), Is.EqualTo("AA"));
        Assert.That(terser.Get("/MSA-2"), Is.EqualTo("CTRL123"));
        Assert.That(terser.Get("/MSH-3"), Is.EqualTo("RCVAPP"));
        Assert.That(terser.Get("/MSH-5"), Is.EqualTo("SNDAPP"));
    }

    [Test]
    public void ShouldReceiveMessageViaMtls()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };

        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = true,
            ListenDurationSeconds = 2,
            BufferSize = 1024,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(200);
            return await SendMessageAsync(port, "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|123|P|2.5", _clientPfxPath, _password);
        });

        var result = Mllp.Receive(input, connection, new Options(), CancellationToken.None);
        var ack = sender.Result;

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Output.First(), Does.Contain("MSH|^~\\&|SENDER"));
            Assert.That(ack, Is.Not.Null.And.Not.Empty);
            Assert.That(ack, Does.Contain("MSH"));
            Assert.That(ack, Does.Contain("MSA|AA"));
            Assert.That(ack, Does.Contain("|123|"));
        });
    }

    [Test]
    public void ShouldNotReceiveMessage_WhenClientCertIsUntrusted_AndIgnoreIsFalse()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = false,
            ListenDurationSeconds = 2,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(200);

            return await SendMessageAsync(port, "MSG|UNTRUSTED", _clientPfxPath, _password);
        });

        var result = Mllp.Receive(input, connection, new Options(), CancellationToken.None);

        Assert.ThrowsAsync<IOException>(async () => await sender);

        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public void ShouldSucceed_WhenClientCertIsUntrusted_ButIgnoreIsTrue()
    {
        var port = GetAvailablePort();
        var input = new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port };
        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = true,
            ListenDurationSeconds = 2,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(200);
            return await SendMessageAsync(port, "MSG|ACCEPTED_BY_IGNORE", _clientPfxPath, _password);
        });

        var result = Mllp.Receive(input, connection, new Options(), CancellationToken.None);

        sender.Wait(TimeSpan.FromSeconds(1));

        Assert.Multiple(() =>
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.Output, Has.Length.EqualTo(1));
            Assert.That(result.Output.First(), Is.EqualTo("MSG|ACCEPTED_BY_IGNORE"));
        });
    }

    private static async Task<string> SendMessageAsync(int port, string message, string clientCertPath = null, string password = null)
    {
        using var client = new TcpClient();
        var attempts = 0;
        while (true)
        {
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
                break;
            }
            catch (SocketException) when (attempts < 5)
            {
                attempts++;
                await Task.Delay(100);
            }
        }

        Stream stream = client.GetStream();

        if (!string.IsNullOrEmpty(clientCertPath))
        {
            var sslStream = new SslStream(stream, false, (sender, cert, chain, errors) => true);
            using var clientCert = new X509Certificate2(clientCertPath, password);
            var clientCerts = new X509Certificate2Collection(clientCert);

            await sslStream.AuthenticateAsClientAsync("localhost", clientCerts, SslProtocols.Tls12, false);
            stream = sslStream;
        }

        var payload = $"\u000b{message}\u001c\r";
        var bytes = Encoding.UTF8.GetBytes(payload);

        await stream.WriteAsync(bytes, 0, bytes.Length);
        await stream.FlushAsync();

        var buffer = new byte[2048];
        var read = await stream.ReadAsync(buffer, 0, buffer.Length);

        if (read <= 0) return string.Empty;

        var ackPayload = Encoding.UTF8.GetString(buffer, 0, read);
        var result = StripMllpFrame(ackPayload);

        if (stream is SslStream ssl)
            ssl.Dispose();

        return result;
    }

    private static string StripMllpFrame(string framed)
    {
        if (string.IsNullOrEmpty(framed))
            return framed;

        var trimmed = framed;
        if (trimmed[0] == '\u000b')
            trimmed = trimmed[1..];
        if (trimmed.EndsWith("\u001c\r", StringComparison.Ordinal))
            trimmed = trimmed[..^2];

        return trimmed;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}