using System;
using System.Diagnostics;
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|ONE|SECURITY|ADT^A01|MSG00001|P|2.5");
        });

        var sender2 = Task.Run(async () =>
        {
            await Task.Delay(150);
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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

            return await SendMessageAsync(
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
        var port = GetAvailablePort();
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

            return await SendMessageAsync(
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
        var port = GetAvailablePort();
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

                return await SendMessageAsync(
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
        var port = GetAvailablePort();
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

            return await SendMessageAsync(
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
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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
            await SendMessageAsync(
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
        var port = GetAvailablePort();
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
        var port = GetAvailablePort();
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

            return await SendMessageAsync(
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

    private static async Task<string> SendMessageAsync(
        int port,
        string message,
        string clientCertPath = null,
        string password = null,
        Encoding encoding = null)
    {
        using var client = new TcpClient();

        for (int i = 0; i < 10; i++)
        {
            try
            {
                await client.ConnectAsync(
                    IPAddress.Loopback,
                    port);

                break;
            }
            catch (SocketException)
            {
                if (i == 9) throw;
                await Task.Delay(500);
            }
        }

        SslStream sslStream = null;

        try
        {
            Stream currentStream = client.GetStream();

            if (!string.IsNullOrEmpty(clientCertPath))
            {
                sslStream = new SslStream(
                    currentStream,
                    true);

                using var clientCert = new X509Certificate2(
                    clientCertPath,
                    password);
                var clientCerts = new X509Certificate2Collection(clientCert);

                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = "localhost",
                    ClientCertificates = clientCerts,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                };

                await sslStream.AuthenticateAsClientAsync(
                    sslOptions,
                    CancellationToken.None);
                currentStream = sslStream;
            }

            var sendEncoding = encoding ?? Encoding.UTF8;
            var payload = $"\u000b{message}\u001c\r";
            var bytes = sendEncoding.GetBytes(payload);

            await currentStream.WriteAsync(
                bytes,
                0,
                bytes.Length);
            await currentStream.FlushAsync();

            var buffer = new byte[8192];
            using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            try
            {
                var read = await currentStream.ReadAsync(
                    buffer,
                    0,
                    buffer.Length,
                    readCts.Token);

                if (read <= 0) return string.Empty;

                var ackPayload = Encoding.UTF8.GetString(
                    buffer,
                    0,
                    read);

                return StripMllpFrame(ackPayload);
            }
            catch (OperationCanceledException)
            {
                return string.Empty;
            }
        }
        finally
        {
            if (sslStream != null) await sslStream.DisposeAsync();
        }
    }

    private static string StripMllpFrame(string framed)
    {
        if (string.IsNullOrEmpty(framed))
            return framed;

        var trimmed = framed;
        if (trimmed[0] == '\u000b')
            trimmed = trimmed[1..];
        if (trimmed.EndsWith(
            "\u001c\r",
            StringComparison.Ordinal))
            trimmed = trimmed[..^2];

        return trimmed;
    }

    private static int GetAvailablePort()
    {
        var listener = new TcpListener(
            IPAddress.Loopback,
            0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
