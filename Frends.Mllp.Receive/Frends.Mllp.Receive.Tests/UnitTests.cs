using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NHapi.Base.Parser;
using NHapi.Base.Util;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class UnitTests
{
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
            await SendMessageAsync(port, "MSG|ONE");
        });

        var sender2 = Task.Run(async () =>
        {
            await Task.Delay(150);
            await SendMessageAsync(port, "MSG|TWO");
        });

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);
        Task.WaitAll(sender1, sender2);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.EquivalentTo(new[] { "MSG|ONE", "MSG|TWO" }));
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

    private static async Task<string> SendMessageAsync(int port, string message)
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
                await Task.Delay(50);
            }
        }

        var payload = $"\u000b{message}\u001c\r";
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var stream = client.GetStream();
        await stream.WriteAsync(bytes, 0, bytes.Length);

        var buffer = new byte[256];
        stream.ReadTimeout = 1000;
        try
        {
            var read = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0)
                return string.Empty;

            var ackPayload = Encoding.UTF8.GetString(buffer, 0, read);
            return StripMllpFrame(ackPayload);
        }
        catch
        {
            return string.Empty;
        }
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