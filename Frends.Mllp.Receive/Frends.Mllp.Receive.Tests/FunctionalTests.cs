using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    private string _clientPfxPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData/client.pfx");

    private string _serverPfxPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "TestData/server.pfx");

    private string _password = "password";

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        // Required for windows-1252 and other non-built-in code-page encodings used in the sender.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Test]
    public async Task ShouldReceiveSingleMessageWithinListenWindow()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 10,
            BufferSize = 1024,
        };
        var options = new Options();

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        await sender;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
        Assert.That(
            result.Output.First(),
            Does.Contain("MSH|^~\\&|HIS|RIH"));
    }

    [Test]
    public async Task ShouldReceiveMultipleMessagesFromMultipleClients()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };
        var options = new Options();

        var sender1 = Task.Run(async () =>
        {
            await Task.Delay(50);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|ONE|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var sender2 = Task.Run(async () =>
        {
            await Task.Delay(150);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|TWO|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        await Task.WhenAll(
            sender1,
            sender2);

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Is.EquivalentTo(new[]
            {
                "MSH|^~\\&|HIS|RIH|EKG|EKG|ONE|SECURITY|ADT^A01|MSG00001|P|2.5",
                "MSH|^~\\&|HIS|RIH|EKG|EKG|TWO|SECURITY|ADT^A01|MSG00001|P|2.5",
            }));
    }

    [Test]
    public async Task ShouldReturnEmptyWhenNoMessagesArrive()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
        };
        var options = new Options();

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Is.Empty);
    }

    [Test]
    public async Task ShouldSendProperAck()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };
        var options = new Options();

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(50);

            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SNDAPP|SNDFAC|RCVAPP|RCVFAC|20250101010101||ORM^O01|CTRL123|P|2.5");
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        var ack = ackTask.Result;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));

        Assert.That(
            ack,
            Is.Not.Null.And.Not.Empty);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(
            terser.Get("/MSH-9-1"),
            Is.EqualTo("ACK"));
        Assert.That(
            terser.Get("/MSH-9-2"),
            Is.EqualTo("O01"));
        Assert.That(
            terser.Get("/MSA-1"),
            Is.EqualTo("AA"));
        Assert.That(
            terser.Get("/MSA-2"),
            Is.EqualTo("CTRL123"));
        Assert.That(
            terser.Get("/MSH-3"),
            Is.EqualTo("RCVAPP"));
        Assert.That(
            terser.Get("/MSH-5"),
            Is.EqualTo("SNDAPP"));
    }

    [Test]
    public async Task ShouldReceiveMessageViaMtls()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = true,
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };

        var serverTask = Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);

        var senderTask = Task.Run(async () =>
        {
            await Task.Delay(500);

            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|123|P|2.5",
                _clientPfxPath,
                _password);
        });

        var ack = await senderTask;

        var result = await serverTask;

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Success,
                Is.True);
            Assert.That(
                result.Output.First(),
                Does.Contain("MSH|^~\\&|SENDER"));
            Assert.That(
                ack,
                Is.Not.Null.And.Not.Empty);
            Assert.That(
                ack,
                Does.Contain("MSA|AA"));
        });
    }

    [Test]
    public async Task ShouldNotReceiveMessage_WhenClientCertIsUntrusted_AndIgnoreIsFalse()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = false,
            ListenDurationSeconds = 10,
            ClientCertificateThumbprints = ["invalid"],
        };

        var serverTask = Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);

        var senderTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(1000);

                return await Helpers.SendMessageAsync(
                    port,
                    "MSG|UNTRUSTED",
                    _clientPfxPath,
                    _password);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client expectedly failed: {ex.Message}");

                return "CLIENT_ERROR_CAUGHT";
            }
        });

        await Task.WhenAll(
            serverTask,
            senderTask).WaitAsync(TimeSpan.FromSeconds(20));

        var result = await serverTask;
        var ack = await senderTask;

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Output,
                Is.Empty,
                "Server should not have accepted the message.");
            Assert.That(
                ack,
                Is.Null.Or.Empty.Or.EqualTo("CLIENT_ERROR_CAUGHT"),
                "Client should not have received a valid MLLP ACK.");
        });
    }

    [Test]
    public async Task ShouldSucceed_WhenClientCertIsUntrusted_ButIgnoreIsTrue()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = true,
            ListenDurationSeconds = 10,
        };

        var serverTask = Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);

        var senderTask = Task.Run(async () =>
        {
            await Task.Delay(500);

            return await Helpers.SendMessageAsync(
                port,
                "MSG|ACCEPTED_BY_IGNORE",
                _clientPfxPath,
                _password);
        });

        await Task.WhenAll(
            serverTask,
            senderTask).WaitAsync(TimeSpan.FromSeconds(20));

        var result = await serverTask;
        var ack = await senderTask;

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Success,
                Is.True);
            Assert.That(
                result.Output,
                Has.Length.EqualTo(1));
            Assert.That(
                result.Output.First(),
                Is.EqualTo("MSG|ACCEPTED_BY_IGNORE"));
            Assert.That(
                ack,
                Is.Not.Null);
        });
    }

    [Test]
    public async Task Test_CheckIfStopAsyncHangsWithActiveZombieClient()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            TlsMode = TlsMode.None,
            ListenDurationSeconds = 2,
        };

        var serverTask = Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);

        var zombieTask = Task.Run(async () =>
        {
            await Task.Delay(500);
            using var client = new TcpClient();
            await client.ConnectAsync(
                IPAddress.Loopback,
                port);
            using var stream = client.GetStream();

            byte[] startByte =
            {
                0x0b,
            };
            await stream.WriteAsync(
                startByte,
                0,
                startByte.Length);

            await Task.Delay(20000);
        });

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await serverTask.WaitAsync(TimeSpan.FromSeconds(15));
            sw.Stop();
            Console.WriteLine($"Server stopped after: {sw.Elapsed.TotalSeconds}s");
        }
        catch (TimeoutException)
        {
            sw.Stop();
            Assert.Fail($"TEST FAILED: Server hangs at StopAsync! " +
                        $"Time elapsed {sw.Elapsed.TotalSeconds} and didnt stopped.");
        }
    }

    [Test]
    public async Task ShouldReceiveMessageWithUtf8Encoding()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            Encoding = FileEncoding.UTF8,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|caf\u00e9|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                encoding: Encoding.UTF8);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);
        await sender;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
        Assert.That(
            result.Output.First(),
            Does.Contain("MSH|^~\\&|HIS|RIH"));
        Assert.That(
            result.Output.First(),
            Does.Contain("caf\u00e9"),
            "UTF-8 encoded 'é' must survive the round-trip.");
    }

    [Test]
    public async Task ShouldReceiveMessageWithAsciiEncoding()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            Encoding = FileEncoding.ASCII,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|caf\u00e9|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                encoding: Encoding.ASCII);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);
        await sender;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
        Assert.That(
            result.Output.First(),
            Does.Contain("MSH|^~\\&|HIS|RIH"));
        Assert.That(
            result.Output.First(),
            Does.Contain("caf?"),
            "ASCII-encoded 'é' must arrive as the replacement character '?'.");
    }

    [Test]
    public async Task ShouldReceiveMessageWithOtherEncodingAsString()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            Encoding = FileEncoding.Other,
            EncodingInString = "iso-8859-1",
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);

            // Encoding.Latin1 is ISO-8859-1 (built-in, no code-page registration required).
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|caf\u00e9|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                encoding: Encoding.Latin1);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);
        await sender;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
        Assert.That(
            result.Output.First(),
            Does.Contain("MSH|^~\\&|HIS|RIH"));
        Assert.That(
            result.Output.First(),
            Does.Contain("caf\u00e9"),
            "ISO-8859-1 encoded 'é' (0xE9) must survive the round-trip.");
    }

    [Test]
    public void ShouldThrowOnInvalidEncoding()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            Encoding = FileEncoding.Other,
            EncodingInString = "not-a-valid-encoding",
        };
        var options = new Options
        {
            ThrowErrorOnFailure = true,
        };

        var ex = Assert.ThrowsAsync<Exception>(() => Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None));
        Assert.That(
            ex,
            Is.Not.Null);
    }

    [Test]
    public async Task ShouldReceiveMessageWithWindows1252Encoding()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            Encoding = FileEncoding.Windows1252,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|caf\u00e9|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                encoding: Encoding.GetEncoding("windows-1252"));
        });

        var result = await Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);
        await sender;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
        Assert.That(
            result.Output.First(),
            Does.Contain("MSH|^~\\&|HIS|RIH"));
        Assert.That(
            result.Output.First(),
            Does.Contain("caf\u00e9"),
            "Windows-1252 encoded 'é' (0xE9) must survive the round-trip.");
    }

    [Test]
    public void ShouldThrowOnEmptyEncodingInString()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            Encoding = FileEncoding.Other,
            EncodingInString = string.Empty,
        };
        var options = new Options
        {
            ThrowErrorOnFailure = true,
        };

        var ex = Assert.ThrowsAsync<Exception>(() => Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None));
        Assert.That(
            ex.Message,
            Does.Contain("EncodingInString"));
    }

    [Test]
    public async Task ShouldSucceed_WhenClientCertThumbprintMatches_AndIgnoreIsFalse()
    {
        using var trustedServerCertificate = Helpers.TrustCertificateForCurrentUserRoot(_serverPfxPath, _password);
        using var trustedClientCertificate = Helpers.TrustCertificateForCurrentUserRoot(_clientPfxPath, _password);

        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };

        using var expectedClientCert = new X509Certificate2(
            _clientPfxPath,
            _password);
        var expectedClientThumbprint = expectedClientCert.GetCertHashString();

        var connection = new Connection
        {
            TlsMode = TlsMode.Mtls,
            ServerCertPath = _serverPfxPath,
            ServerCertPassword = _password,
            IgnoreClientCertificateErrors = false,
            ClientCertificateThumbprints =
            [
                expectedClientThumbprint,
            ],
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };

        var serverTask = Mllp.Receive(
            input,
            connection,
            new Options(),
            CancellationToken.None);

        var senderTask = Task.Run(async () =>
        {
            await Task.Delay(1000);

            return await Helpers.SendMessageAsync(
                port,
                "MSG|PINNED_CERT_OK",
                _clientPfxPath,
                _password);
        });

        await Task.WhenAll(
            serverTask,
            senderTask).WaitAsync(TimeSpan.FromSeconds(20));

        var result = await serverTask;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ShouldReceiveMessageWithCustomFramingBytes()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 10,
            BufferSize = 1024,
        };
        var options = new Options
        {
            StartBlockByte = 1,
            EndBlockByte = 2,
            CarriageReturnByte = 3,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                startBlock: 1,
                endBlock: 2,
                carriageReturn: 3);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(result.Output.First(), Does.Contain("MSH|^~\\&|HIS|RIH"));
    }

    [Test]
    public async Task ShouldSendControlByteAcknowledgementWithCustomFramingBytes()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 10,
            BufferSize = 1024,
        };
        var options = new Options
        {
            StartBlockByte = 1,
            EndBlockByte = 2,
            CarriageReturnByte = 13,
            AcknowledgementFormat = AcknowledgementFormat.ControlByte,
        };

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAndReadAcknowledgementBytesAsync(
                port,
                "MSH|^~\\&|SNDAPP|SNDFAC|RCVAPP|RCVFAC|20250101010101||ORM^O01|CTRL123|P|2.5",
                startBlock: 1,
                endBlock: 2,
                carriageReturn: 13);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        var acknowledgementBytes = await ackTask;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(acknowledgementBytes, Is.EqualTo(new byte[] { 0x01, 0x06, 0x02, 0x0D }));
    }

    [Test]
    public async Task ShouldRejectConnectionWhenMaxConcurrentConnectionsExceeded()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}.log");
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 6,
            BufferSize = 1024,
            SendAcknowledgement = false,
        };
        var options = new Options
        {
            MaxConcurrentConnections = 2,
            EnableLogging = true,
            LogFilePath = logPath,
        };

        var firstTwoConnectionsEstablished = new TaskCompletionSource<bool>();
        var connectionCount = 0;

        var sender1 = Task.Run(async () =>
        {
            await Task.Delay(100);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            var startBytes = new byte[] { 0x0b };
            await stream.WriteAsync(startBytes, 0, startBytes.Length);
            await stream.FlushAsync();

            if (Interlocked.Increment(ref connectionCount) == 2)
                firstTwoConnectionsEstablished.SetResult(true);

            await Task.Delay(2000);

            var msgBytes = Encoding.UTF8.GetBytes("MSH|^~\\&|FIRST\u001c\r");
            await stream.WriteAsync(msgBytes, 0, msgBytes.Length);
            await stream.FlushAsync();
            await Task.Delay(200);
        });

        var sender2 = Task.Run(async () =>
        {
            await Task.Delay(150);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            var startBytes = new byte[] { 0x0b };
            await stream.WriteAsync(startBytes, 0, startBytes.Length);
            await stream.FlushAsync();

            if (Interlocked.Increment(ref connectionCount) == 2)
                firstTwoConnectionsEstablished.SetResult(true);

            await Task.Delay(2000);

            var msgBytes = Encoding.UTF8.GetBytes("MSH|^~\\&|SECOND\u001c\r");
            await stream.WriteAsync(msgBytes, 0, msgBytes.Length);
            await stream.FlushAsync();
            await Task.Delay(200);
        });

        var sender3 = Task.Run(async () =>
        {
            await firstTwoConnectionsEstablished.Task;
            await Task.Delay(300);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                using var stream = client.GetStream();

                var msgBytes = Encoding.UTF8.GetBytes("\u000bMSH|^~\\&|THIRD\u001c\r");
                await stream.WriteAsync(msgBytes, 0, msgBytes.Length);
                await stream.FlushAsync();
                await Task.Delay(500);
            }
            catch
            {
                // Ignore - connection may be closed
            }
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await Task.WhenAll(sender1, sender2, sender3);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output.Length, Is.EqualTo(2), "Only two messages should be received");

        var logContent = await File.ReadAllTextAsync(logPath);
        Assert.That(logContent, Does.Contain("CONNECTION REJECTED"));
        Assert.That(logContent, Does.Contain("Max concurrent connections limit"));

        File.Delete(logPath);
    }

    [Test]
    public async Task ShouldAcceptAllConnectionsWhenMaxConcurrentConnectionsIsZero()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 10,
            BufferSize = 1024,
        };
        var options = new Options
        {
            MaxConcurrentConnections = 0,
        };

        var serverTask = Mllp.Receive(input, connection, options, CancellationToken.None);

        await Task.Delay(500);

        var senderTasks = Enumerable.Range(0, 5).Select(i => Task.Run(async () =>
        {
            await Task.Delay(i * 100);
            await Helpers.SendMessageAsync(
                port,
                $"MSH|^~\\&|MSG{i}|FAC|RCV|FAC|20250101||ADT^A01|ID{i}|P|2.5");
        })).ToArray();

        await Task.WhenAll(senderTasks);

        await Task.Delay(500);

        var result = await serverTask;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(5), "All 5 messages should be accepted when MaxConcurrentConnections is 0 (unlimited)");

        for (int i = 0; i < 5; i++)
        {
            Assert.That(result.Output, Has.Some.Contains($"MSG{i}"), $"Message {i} should be present in output");
        }
    }

    [Test]
    public async Task ShouldRejectMessageExceedingMaxMessageSize()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 4096,
        };
        var options = new Options
        {
            MaxMessageSize = 50,
        };

        var largeMessage = "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5|" +
                           new string('X', 100);

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(port, largeMessage);
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
    }

    [Test]
    public async Task ShouldAcceptMessageWithinMaxMessageSize()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 4096,
        };
        var options = new Options
        {
            MaxMessageSize = 500,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task ShouldReceiveMessageWithoutCarriageReturnWhenNotRequired()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            SendAcknowledgement = false,
        };
        var options = new Options
        {
            CarriageReturnRequired = false,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            var payload = Encoding.UTF8.GetBytes(
                "MSH|^~\\&|HIS|RIH|EKG|EKG|199904140038||ADT^A01|MSG00001|P|2.5");
            var bytes = new byte[payload.Length + 2];
            bytes[0] = 0x0b;
            Buffer.BlockCopy(payload, 0, bytes, 1, payload.Length);
            bytes[^1] = 0x1c;

            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();
            await Task.Delay(500);
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(result.Output.First(), Does.Contain("MSH|^~\\&|HIS|RIH"));
    }

    [Test]
    public async Task ShouldLogMessageEventsToFile()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}.log");
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };
        var options = new Options
        {
            EnableLogging = true,
            LogFilePath = logPath,
            LogMessageContent = true,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|199904140038||ADT^A01|MSG00001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(logPath), Is.True, "Log file should be created");

        var logContent = await File.ReadAllTextAsync(logPath);
        Assert.That(logContent, Does.Contain("MESSAGE RECEIVED"));
        Assert.That(logContent, Does.Contain("SUCCESS"));

        File.Delete(logPath);
    }

    [Test]
    public async Task ShouldLogRejectedMessageWhenExceedingSizeLimit()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}.log");
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 4096,
        };
        var options = new Options
        {
            MaxMessageSize = 20,
            EnableLogging = true,
            LogFilePath = logPath,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|199904140038||ADT^A01|MSG00001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
        Assert.That(File.Exists(logPath), Is.True);

        var logContent = await File.ReadAllTextAsync(logPath);
        Assert.That(logContent, Does.Contain("MESSAGE REJECTED"));
        Assert.That(logContent, Does.Contain("exceeds limit"));

        File.Delete(logPath);
    }

    [Test]
    public async Task ShouldSendControlByteNackWhenMessageExceedsSize()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 4096,
        };
        var options = new Options
        {
            MaxMessageSize = 20,
            AcknowledgementFormat = AcknowledgementFormat.ControlByte,
        };

        var largeMessage = "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5|" +
                           new string('X', 100);

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAndReadAcknowledgementBytesAsync(
                port,
                largeMessage,
                0x0b,
                0x1c,
                0x0d);
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ackBytes = await ackTask;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
        Assert.That(ackBytes, Is.EqualTo(new byte[] { 0x0b, 0x15, 0x1c, 0x0d }), "Should send NACK (0x15) for rejected message");
    }

    [Test]
    public async Task ShouldSendHl7NackWithErrorDescriptionWhenMessageExceedsSize()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 4096,
        };
        var options = new Options
        {
            MaxMessageSize = 20,
            AcknowledgementFormat = AcknowledgementFormat.Hl7,
        };

        var largeMessage = "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5|" +
                           new string('X', 100);

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(port, largeMessage);
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ackMessage = await ackTask;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Is.Empty);
        Assert.That(ackMessage, Does.Contain("MSA|AE"));
        Assert.That(ackMessage, Does.Contain("Message too large"), "NACK should contain error description in MSA-3");
    }

    [Test]
    public async Task ShouldUseConfiguredAckSenderApplication()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            AckSenderApplication = "CUSTOM_ACK_SENDER",
        };
        var options = new Options();

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|MSG001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ack = await ackTask;

        Assert.That(result.Success, Is.True);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(terser.Get("/MSH-3"), Is.EqualTo("CUSTOM_ACK_SENDER"), "ACK should use configured sender application");
    }

    [Test]
    public async Task ShouldUseConfiguredAckReceiverApplication()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            AckReceiverApplication = "CUSTOM_ACK_RECEIVER",
        };
        var options = new Options();

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|MSG001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ack = await ackTask;

        Assert.That(result.Success, Is.True);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(terser.Get("/MSH-5"), Is.EqualTo("CUSTOM_ACK_RECEIVER"), "ACK should use configured receiver application");
    }

    [Test]
    public async Task ShouldUseConfiguredHl7Version()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            AckHl7Version = "2.3.1",
        };
        var options = new Options();

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|MSG001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ack = await ackTask;

        Assert.That(result.Success, Is.True);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(terser.Get("/MSH-12"), Is.EqualTo("2.3.1"), "ACK should use configured HL7 version");
    }

    [Test]
    public async Task ShouldSendAckWithConfiguredAckType()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            AcknowledgementType = AcknowledgementType.AE, // Application Error
        };
        var options = new Options();

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            return await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|SENDER|FAC|RECEIVER|FAC|20250101||ADT^A01|MSG001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ack = await ackTask;

        Assert.That(result.Success, Is.True);

        var parser = new PipeParser();
        var ackMessage = parser.Parse(ack);
        var terser = new Terser(ackMessage);

        Assert.That(terser.Get("/MSA-1"), Is.EqualTo("AE"), "ACK should use configured type AE (Application Error)");
    }

    [Test]
    public async Task ShouldSendControlByteAckWithoutCarriageReturnWhenNotRequired()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
        };
        var options = new Options
        {
            AcknowledgementFormat = AcknowledgementFormat.ControlByte,
            CarriageReturnRequired = false,
        };

        var ackTask = Task.Run(async () =>
        {
            await Task.Delay(100);
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            // Send message without CR
            var payload = Encoding.UTF8.GetBytes("MSH|^~\\&|APP|FAC|RCV|FAC|20250101||ADT^A01|MSG001|P|2.5");
            var msgBytes = new byte[payload.Length + 2];
            msgBytes[0] = 0x0b;
            Buffer.BlockCopy(payload, 0, msgBytes, 1, payload.Length);
            msgBytes[^1] = 0x1c; // Only EB, no CR

            await stream.WriteAsync(msgBytes, 0, msgBytes.Length);
            await stream.FlushAsync();

            var ackBuffer = new byte[10];
            var read = await stream.ReadAsync(ackBuffer, 0, ackBuffer.Length);
            return ackBuffer[..read];
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        var ackBytes = await ackTask;

        Assert.That(result.Success, Is.True);
        Assert.That(ackBytes, Is.EqualTo(new byte[] { 0x0b, 0x06, 0x1c }), "ACK should not include CR when CarriageReturnRequired = false");
    }

    [Test]
    public async Task ShouldGenerateAckFromMultiSegmentMessageUsingOnlyMsh()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            SendAcknowledgement = true,
            AcknowledgementType = 0,
        };
        var options = new Options
        {
            StartBlockByte = 0x0b,
            EndBlockByte = 0x1c,
            CarriageReturnByte = 0x0d,
            CarriageReturnRequired = true,
        };

        var rawHl7Message =
            "MSH|^~\\&|HIS|RIH|EKG|EKG|20260611||ADT^A01|MSG00002|P|2.5\r" +
            "EVN|A01|202606111100\r" +
            "PID|||12345^^^MRN||Kowalski|Jan||19800101|M\r" +
            "PV1||I|Internal Medicine||||||||||||||||1001";

        string receivedAck = null;

        var sender = Task.Run(async () =>
        {
            await Task.Delay(200);

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            var messageBytes = Encoding.UTF8.GetBytes(rawHl7Message);
            var framed = new byte[messageBytes.Length + 3];
            framed[0] = options.StartBlockByte;
            Buffer.BlockCopy(messageBytes, 0, framed, 1, messageBytes.Length);
            framed[^2] = options.EndBlockByte;
            framed[^1] = options.CarriageReturnByte;

            await stream.WriteAsync(framed, 0, framed.Length);
            await stream.FlushAsync();

            var buffer = new byte[2048];
            var readBytes = await stream.ReadAsync(buffer, 0, buffer.Length);

            if (readBytes > 0)
            {
                var rawResponse = Encoding.UTF8.GetString(buffer, 0, readBytes);
                var startIdx = rawResponse.IndexOf((char)options.StartBlockByte);
                var endIdx = rawResponse.IndexOf((char)options.EndBlockByte);

                if (startIdx >= 0 && endIdx > startIdx)
                {
                    receivedAck = rawResponse.Substring(startIdx + 1, endIdx - startIdx - 1);
                }
            }
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(receivedAck, Is.Not.Null, "The server failed to return an ACK response.");
        Assert.That(receivedAck, Does.Contain("MSA|AA|MSG00002"));
        Assert.That(receivedAck, Does.StartWith("MSH|^~\\&"));
    }

    [Test]
    public async Task ShouldWriteMessagesToFileWhenEnabled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}");
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            SendAcknowledgement = false,
        };
        var options = new Options
        {
            WriteMessagesToFile = true,
            MessageOutputDirectory = tempDir,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|20250101||ADT^A01|MSG001|P|2.5");
        });

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(File.Exists(result.Output[0]), Is.True, "Output should be a valid file path");

        var content = await File.ReadAllTextAsync(result.Output[0]);
        Assert.That(content, Does.StartWith("MSH|"));

        Directory.Delete(tempDir, recursive: true);
    }

    [Test]
    public async Task ShouldWriteMultipleMessagesToSeparateFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}");
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 5,
            BufferSize = 1024,
            SendAcknowledgement = false,
        };
        var options = new Options
        {
            WriteMessagesToFile = true,
            MessageOutputDirectory = tempDir,
        };

        var senders = Enumerable.Range(0, 3).Select(i => Task.Run(async () =>
        {
            await Task.Delay(100 + (i * 100));
            await Helpers.SendMessageAsync(
                port,
                $"MSH|^~\\&|HIS|RIH|EKG|EKG|20250101||ADT^A01|MSG00{i}|P|2.5");
        })).ToArray();

        var result = await Mllp.Receive(input, connection, options, CancellationToken.None);
        await Task.WhenAll(senders);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(3));
        Assert.That(result.Output, Is.Unique, "Each message should have a separate file");
        Assert.That(result.Output.All(File.Exists), Is.True, "All output paths should exist");

        Directory.Delete(tempDir, recursive: true);
    }

    [Test]
    public async Task ShouldHandleMultipleLargeMessagesWrittenToFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"mllp-test-{Guid.NewGuid()}");
        var (result, memoryUsedMB) = await RunLargeMessageTest(Helpers.GetAvailablePort(), writeToFile: true, tempDir);

        TestContext.WriteLine($"Memory delta (file mode): {memoryUsedMB:F2} MB");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output.Length, Is.EqualTo(3));
        Assert.That(result.Output.All(File.Exists), Is.True);
        Assert.That(result.Output.All(p => new FileInfo(p).Length > 50 * 1024 * 1024), Is.True);

        Directory.Delete(tempDir, recursive: true);
    }

    [Test]
    public async Task ShouldHandleMultipleLargeMessagesInMemory()
    {
        var (result, memoryUsedMB) = await RunLargeMessageTest(Helpers.GetAvailablePort(), writeToFile: false);

        TestContext.WriteLine($"Memory delta (memory mode): {memoryUsedMB:F2} MB");

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output.Length, Is.EqualTo(3));
        Assert.That(result.Output.All(m => m.StartsWith("MSH|")), Is.True);
        Assert.That(result.Output.All(m => m.Length > 50 * 1024 * 1024), Is.True);
    }

    private static async Task<(Result result, double memoryUsedMB)> RunLargeMessageTest(
        int port,
        bool writeToFile,
        string tempDir = null)
    {
        var largePayload = "MSH|^~\\&|HIS|RIH|EKG|EKG|20250101||ADT^A01|MSG001|P|2.5\r" + new string('X', 50 * 1024 * 1024);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryBefore = Process.GetCurrentProcess().PrivateMemorySize64;

        var serverTask = Mllp.Receive(
            new Input { ListenAddress = IPAddress.Loopback.ToString(), Port = port },
            new Connection { ListenDurationSeconds = 90, BufferSize = 256 * 1024, SendAcknowledgement = false },
            new Options { WriteMessagesToFile = writeToFile, MessageOutputDirectory = tempDir ?? string.Empty },
            CancellationToken.None);

        await Task.Delay(500);

        await Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(async () =>
        {
            await Task.Delay(100 + (i * 500));
            using var client = new TcpClient { SendTimeout = 120000, ReceiveTimeout = 120000 };
            await client.ConnectAsync(IPAddress.Loopback, port);
            using var stream = client.GetStream();

            var messageBytes = Encoding.UTF8.GetBytes(largePayload);
            var framed = new byte[messageBytes.Length + 3];
            framed[0] = 0x0b;
            Buffer.BlockCopy(messageBytes, 0, framed, 1, messageBytes.Length);
            framed[^2] = 0x1c;
            framed[^1] = 0x0d;

            await stream.WriteAsync(framed, 0, framed.Length);
            await stream.FlushAsync();
            await Task.Delay(2000);
        })));

        var result = await serverTask;

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memoryUsedMB = (Process.GetCurrentProcess().PrivateMemorySize64 - memoryBefore) / (1024.0 * 1024.0);

        return (result, memoryUsedMB);
    }
}