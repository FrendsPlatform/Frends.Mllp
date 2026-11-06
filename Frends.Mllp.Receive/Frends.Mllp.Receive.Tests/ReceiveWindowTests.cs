using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using Frends.Mllp.Receive.Helpers;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class ReceiveWindowTests
{
    private const string Hl7Message = "MSH|^~\\&|HIS|RIH|EKG|EKG|20250101||ADT^A01|MSG001|P|2.5";

    [Test]
    public async Task Should_Not_Acknowledge_Or_Return_Message_Arriving_After_Window_Closed()
    {
        var port = Helpers.GetAvailablePort();

        // A closed collector simulates a message that arrives after the result was built, while the listener
        // is still shutting down.
        var collector = new MessageCollector();
        collector.Close();

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(port, Hl7Message);
        });

        var result = await Mllp.ReceiveCore(Input(port), Connection(), new Options(), collector, CancellationToken.None);
        var ack = await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
        Assert.That(ack, Is.Empty, "A refused message must not be acknowledged.");
    }

    [Test]
    public async Task Should_Delete_Message_File_Arriving_After_Window_Closed()
    {
        var port = Helpers.GetAvailablePort();
        var tempDir = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}");
        var collector = new MessageCollector();
        collector.Close();

        var options = new Options
        {
            WriteMessagesToFile = true,
            MessageOutputDirectory = tempDir,
        };

        try
        {
            var sender = Task.Run(async () =>
            {
                await Task.Delay(100);
                return await Helpers.SendMessageAsync(port, Hl7Message);
            });

            var result = await Mllp.ReceiveCore(Input(port), Connection(), options, collector, CancellationToken.None);
            var ack = await sender;

            Assert.That(result.Success, Is.True);
            Assert.That(result.Output, Is.Empty);
            Assert.That(ack, Is.Empty, "A refused message must not be acknowledged.");
            Assert.That(
                Directory.Exists(tempDir) ? Directory.GetFiles(tempDir) : [],
                Is.Empty,
                "The file of a refused message must be deleted.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task Should_Return_Original_Exception_Without_Warnings_When_Port_In_Use()
    {
        var occupier = new TcpListener(IPAddress.Loopback, 0);
        occupier.Start();
        var port = ((IPEndPoint)occupier.LocalEndpoint).Port;

        try
        {
            var options = new Options { ThrowErrorOnFailure = false };

            var result = await Mllp.Receive(Input(port), Connection(), options, CancellationToken.None);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error.AdditionalInfo, Is.Not.Null);
            Assert.That(result.Error.Message, Is.EqualTo(result.Error.AdditionalInfo.Message));
            Assert.That(result.Warnings, Is.Empty, "A host that failed to start must not report a failed stop.");
        }
        finally
        {
            occupier.Stop();
        }
    }

    [Test]
    public async Task Should_Reject_Listen_Duration_Longer_Than_Task_Delay_Allows()
    {
        var connection = Connection();
        connection.ListenDurationSeconds = 2147484;

        var result = await Mllp.Receive(
            Input(Helpers.GetAvailablePort()),
            connection,
            new Options { ThrowErrorOnFailure = false },
            CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error.Message, Does.Contain("Listen duration"));
    }

    private static Input Input(int port) => new()
    {
        ListenAddress = IPAddress.Loopback.ToString(),
        Port = port,
    };

    private static Connection Connection() => new()
    {
        ListenDurationSeconds = 2,
        BufferSize = 1024,
    };
}
