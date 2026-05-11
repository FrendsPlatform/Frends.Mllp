using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Send.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Send.Tests;

[TestFixture]
public class OptionsTests
{
    private TcpListener _listener;
    private int _port;
    private CancellationTokenSource _serverCts;

    [SetUp]
    public void SetUp()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverCts = new CancellationTokenSource();
    }

    [TearDown]
    public void TearDown()
    {
        _serverCts.Cancel();
        _listener.Stop();
        _serverCts.Dispose();
    }

    [Test]
    public async Task ShouldSendMessageWithCustomFramingBytes()
    {
        const byte startBlock = 0x01;
        const byte endBlock = 0x02;
        const byte carriageReturn = 0x03;

        var serverTask = Task.Run(async () =>
        {
            using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
            var stream = client.GetStream();
            var received = await Helpers.ReadMllpMessage(
                stream,
                Encoding.ASCII,
                _serverCts.Token,
                startBlock,
                endBlock);
            var ack = Helpers.BuildAck(Helpers.ExtractControlId(received));
            var ackBytes = Encoding.ASCII.GetBytes(ack);
            var framed = new byte[ackBytes.Length + 3];
            framed[0] = startBlock;
            Buffer.BlockCopy(ackBytes, 0, framed, 1, ackBytes.Length);
            framed[^2] = endBlock;
            framed[^1] = carriageReturn;
            await stream.WriteAsync(framed, 0, framed.Length);
            await stream.FlushAsync();
            return received;
        });

        var result = Mllp.Send(
            new Input { Hl7Message = Helpers.BuildTestMessage() },
            new Connection
            {
                Host = "127.0.0.1",
                Port = _port,
                TlsMode = TlsMode.None,
                ConnectTimeoutSeconds = 5,
            },
            new Options
            {
                ExpectAcknowledgement = true,
                StartBlockByte = startBlock,
                EndBlockByte = endBlock,
                CarriageReturnByte = carriageReturn,
            },
            CancellationToken.None);

        var receivedByServer = await serverTask;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Does.Contain("MSA|AA"));
        Assert.That(receivedByServer, Does.Contain("MSG00001"));
    }
}
