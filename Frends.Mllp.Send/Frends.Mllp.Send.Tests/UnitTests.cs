using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Send.Definitions;
using NHapiTools.Base.Util;
using NUnit.Framework;

namespace Frends.Mllp.Send.Tests;

[TestFixture]
public class UnitTests
{
    [Test]
    public void ShouldSendHl7MessageAndReceiveAck()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var serverTask = Task.Run(
            async () =>
            {
                try
                {
                    TestContext.Progress.WriteLine("Server waiting for client");
                    using var client = await listener.AcceptTcpClientAsync(cts.Token);
                    using var stream = client.GetStream();
                    var receivedMessage = await ReadMllpMessage(stream, cts.Token);
                    TestContext.Progress.WriteLine($"Server received message: {receivedMessage}");
                    var controlId = ExtractControlId(receivedMessage);
                    var ackPayload = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(BuildAck(controlId)));
                    TestContext.Progress.WriteLine($"ACK payload: {BitConverter.ToString(ackPayload)}");
                    await stream.WriteAsync(ackPayload, 0, ackPayload.Length, cts.Token);
                    await stream.FlushAsync(cts.Token);
                    TestContext.Progress.WriteLine("Server sent ACK");
                    return receivedMessage;
                }
                catch (Exception ex)
                {
                    TestContext.Error.WriteLine($"Server task failed: {ex}");
                    throw;
                }
            },
            cts.Token);

        var input = new Input { Hl7Message = BuildTestMessage() };
        var connection = new Connection { Host = "127.0.0.1", Port = port, ConnectTimeoutSeconds = 5, ReadTimeoutSeconds = 5 };
        var options = new Options { ExpectAcknowledgement = true, ValidateWithNhapi = true };

        var result = Mllp.Send(input, connection, options, CancellationToken.None);
        var received = serverTask.GetAwaiter().GetResult();

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain("MSA|AA|MSG00001"));
        Assert.That(received, Does.Contain("MSG00001"));
    }

    private static async Task<string> ReadMllpMessage(NetworkStream stream, CancellationToken cancellationToken)
    {
        const byte startBlock = 0x0b;
        const byte endBlock = 0x1c;
        const byte carriageReturn = 0x0d;

        var buffer = new byte[256];
        var builder = new StringBuilder();
        var started = false;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                throw new Exception("Connection closed unexpectedly.");

            for (var i = 0; i < read; i++)
            {
                var current = buffer[i];
                if (current == startBlock)
                {
                    builder.Append((char)current);
                    started = true;
                    continue;
                }

                if (!started)
                    continue;

                if (current == endBlock)
                {
                    builder.Append((char)current);

                    // Consume trailing CR
                    if (i + 1 >= read || buffer[i + 1] != carriageReturn)
                        _ = await stream.ReadAsync(buffer, 0, 1, cancellationToken);
                    else
                        builder.Append((char)carriageReturn);

                    var sb = new StringBuilder(builder.ToString());
                    MLLP.StripMLLPContainer(sb);
                    return sb.ToString();
                }

                builder.Append((char)current);
            }
        }
    }

    private static string BuildTestMessage() =>
        "MSH|^~\\&|SendingApp|SendingFac|ReceivingApp|ReceivingFac|20250101010101||ADT^A01|MSG00001|P|2.5.1\r" +
        "EVN|A01|20250101010101\r" +
        "PID|1||12345^^^Hospital^MR||Doe^John||19800101|M";

    private static string BuildAck(string controlId) =>
        $"MSH|^~\\&|Listener|ListenerFacility|Sender|SenderFacility|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK^A01|ACK0001|P|2.5.1\r" +
        $"MSA|AA|{controlId}\r";

    private static string ExtractControlId(string hl7Message)
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
}
