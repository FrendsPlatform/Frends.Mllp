using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class UnitTests
{
    [Test]
    public void ShouldReceiveSingleMessageWithinListenWindow()
    {
        var port = GetAvailablePort();
        var input = new Input { Port = port };
        var connection = new Connection { ListenAddress = IPAddress.Loopback.ToString() };
        var options = new Options { ListenDurationSeconds = 2, BufferSize = 1024 };

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
        var input = new Input { Port = port };
        var connection = new Connection { ListenAddress = IPAddress.Loopback.ToString() };
        var options = new Options { ListenDurationSeconds = 3, BufferSize = 1024 };

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
        var input = new Input { Port = port };
        var connection = new Connection { ListenAddress = IPAddress.Loopback.ToString() };
        var options = new Options { ListenDurationSeconds = 1 };

        var result = Mllp.Receive(input, connection, options, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
    }

    private static async Task SendMessageAsync(int port, string message)
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

        // Try to read ACK if it arrives, but do not block the test.
        var buffer = new byte[128];
        stream.ReadTimeout = 250;
        try
        {
            await stream.ReadAsync(buffer, 0, buffer.Length);
        }
        catch
        {
            // Ignore timeouts or disconnects while waiting for ACK.
        }
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
