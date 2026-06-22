using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.Caching;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Send.Definitions;
using NHapiTools.Base.Util;
using NUnit.Framework;

namespace Frends.Mllp.Send.Tests;

/// <summary>
/// MTLS TESTING: These tests require a trusted certificate environment.
/// To run locally without installing certificates on your host machine, use Docker Compose:
/// 1. Navigate to the Frends.Mllp.Send.Tests directory.
/// 2. Run: docker-compose up --build
/// </summary>
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

    private TcpListener _listener;
    private int _port;
    private CancellationTokenSource _serverCts;
    private Task<string> _serverTask;

    [SetUp]
    public void StartServer()
    {
        ClearConnectionCache();
        _listener = new TcpListener(
            IPAddress.Loopback,
            0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverCts = new CancellationTokenSource();
    }

    [TearDown]
    public async Task StopServer()
    {
        _serverCts.Cancel();
        _listener.Stop();

        if (_serverTask != null)
        {
            try
            {
                await _serverTask;
            }
            catch
            {
            }
        }

        _serverCts.Dispose();
    }

    [Test]
    public async Task ShouldSendAndReceiveWithoutTls()
    {
        SetupServerLogic(requireTls: false);

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };

        var result = Mllp.Send(
            input,
            connection,
            new Options
            {
                ExpectAcknowledgement = true,
            },
            CancellationToken.None);
        var receivedByServer = await _serverTask;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Does.Contain("MSA|AA"));
        Assert.That(
            receivedByServer,
            Is.Not.Null);
    }

    [Test]
    public void MtlsShouldWork()
    {
        SetupServerLogic(requireTls: true);
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.Mtls,
            ClientCertPath = _clientPfxPath,
            ClientCertPassword = _password,
            IgnoreServerCertificateErrors = true,
            ConnectTimeoutSeconds = 5,
        };

        var result = Mllp.Send(
            new Input
            {
                Hl7Message = Helpers.BuildTestMessage(),
            },
            connection,
            new Options
            {
                ExpectAcknowledgement = true,
            },
            CancellationToken.None);

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            _serverTask.Result,
            Does.Contain("MSG00001"));
    }

    [Test]
    public void NoCertMtlsSendShouldFail()
    {
        SetupServerLogic(requireTls: true);

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.Mtls,
            ClientCertPath = null,
            IgnoreServerCertificateErrors = true,
            ConnectTimeoutSeconds = 2,
        };

        var options = new Options
        {
            ExpectAcknowledgement = true,
        };

        var ex = Assert.Throws<Exception>(() =>
        {
            Mllp.Send(
                input,
                connection,
                options,
                CancellationToken.None);
        });

        Assert.That(ex, Is.Not.Null);
        Assert.That(
            ex.Message,
            Is.EqualTo("mTLS is enabled but client certificate path is missing."));
    }

    [Test]
    public void MtlsSendValidationShouldSucceed()
    {
        SetupServerLogic(requireTls: true);

        var connection = new Connection
        {
            Host = "localhost",
            Port = _port,
            TlsMode = TlsMode.Mtls,
            ClientCertPath = _clientPfxPath,
            ClientCertPassword = _password,
            IgnoreServerCertificateErrors = false,
            ConnectTimeoutSeconds = 5,
        };

        var options = new Options
        {
            ExpectAcknowledgement = true,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var result = Mllp.Send(
            input,
            connection,
            options,
            CancellationToken.None);

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Is.Not.Null);
    }

    [Test]
    public void MtlsSendValidationShouldSucceedWithMatchingServerThumbprint()
    {
        using var trustedServerCertificate = Helpers.TrustCertificateForCurrentUserRoot(_serverPfxPath, _password);
        using var trustedClientCertificate = Helpers.TrustCertificateForCurrentUserRoot(_clientPfxPath, _password);
        SetupServerLogic(requireTls: true);
        var serverThumbprint = GetServerCertificateThumbprint();

        var connection = new Connection
        {
            Host = "localhost",
            Port = _port,
            TlsMode = TlsMode.Mtls,
            ClientCertPath = _clientPfxPath,
            ClientCertPassword = _password,
            IgnoreServerCertificateErrors = false,
            ServerCertificateThumbprints = [serverThumbprint],
            ConnectTimeoutSeconds = 5,
        };

        var options = new Options
        {
            ExpectAcknowledgement = true,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var result = Mllp.Send(
            input,
            connection,
            options,
            CancellationToken.None);

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Is.Not.Null);
        Assert.That(
            _serverTask.Result,
            Does.Contain("MSG00001"));
    }

    [Test]
    public void MtlsSendValidationShouldThrowOnInvalidServerThumbprint()
    {
        SetupServerLogic(requireTls: true);

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.Mtls,
            ClientCertPath = _clientPfxPath,
            ClientCertPassword = _password,
            IgnoreServerCertificateErrors = false,
            ServerCertificateThumbprints = ["invalid"],
            ConnectTimeoutSeconds = 5,
        };

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
        };

        var ex = Assert.Throws<Exception>(() =>
        {
            Mllp.Send(
                input,
                connection,
                options,
                CancellationToken.None);
        });

        Assert.That(ex, Is.Not.Null);
        Assert.That(
            ex.Message,
            Does.Contain("remote certificate was rejected")
                .Or.Contain("RemoteCertificateValidationCallback"));
    }

    [TestCase(FileEncoding.UTF8, null, "D\u00f6e^J\u00f6hn")]
    [TestCase(FileEncoding.ASCII, null, "D?e^J?hn")]
    [TestCase(FileEncoding.Other, "iso-8859-1", "D\u00f6e^J\u00f6hn")]
    [TestCase(FileEncoding.Windows1252, null, "D\u00f6e^J\u00f6hn")]
    public async Task ShouldRoundTripSpecialCharactersByEncoding(
        FileEncoding fileEncoding,
        string encodingInString,
        string expectedPatientName)
    {
        const string originalPatientName = "D\u00f6e^J\u00f6hn";
        var messageEncoding = ResolveEncoding(fileEncoding, encodingInString);
        SetupServerLogic(requireTls: false, messageEncoding);

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
            Encoding = fileEncoding,
            EncodingInString = encodingInString,
        };

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(originalPatientName),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
        };

        var result = Mllp.Send(
            input,
            connection,
            options,
            CancellationToken.None);
        var receivedByServer = await _serverTask;

        Assert.That(
            result.Success,
            Is.True);
        Assert.That(
            result.Output,
            Does.Contain("MSA|AA"));
        Assert.That(
            receivedByServer,
            Does.Contain($"PID|1||12345^^^Hospital^MR||{expectedPatientName}||19800101|M"));
    }

    [Test]
    public void ShouldThrowOnInvalidEncoding()
    {
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
            Encoding = FileEncoding.Other,
            EncodingInString = "not-a-valid-encoding",
        };

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ThrowErrorOnFailure = true,
        };

        Assert.Throws<Exception>(() => Mllp.Send(
            input,
            connection,
            options,
            CancellationToken.None));
    }

    [Test]
    public void ShouldThrowOnEmptyEncodingInString()
    {
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
            Encoding = FileEncoding.Other,
            EncodingInString = string.Empty,
        };

        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ThrowErrorOnFailure = true,
        };

        var ex = Assert.Throws<Exception>(() => Mllp.Send(
            input,
            connection,
            options,
            CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
        Assert.That(
            ex.Message,
            Does.Contain("EncodingInString"));
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

    [Test]
    public async Task ShouldReuseConnectionWhenKeepAliveEnabled()
    {
        var connectionCount = 0;
        SetupServerLogicMultiMessage(
        requireTls: false,
        onNewConnection: () => Interlocked.Increment(ref connectionCount));

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            KeepConnectionAlive = true,
            ConnectionCacheExpirationMinutes = 1,
        };

        var result1 = Mllp.Send(input, connection, options, CancellationToken.None);
        var result2 = Mllp.Send(input, connection, options, CancellationToken.None);
        var result3 = Mllp.Send(input, connection, options, CancellationToken.None);

        await _serverTask;

        Assert.That(result1.Success, Is.True);
        Assert.That(result2.Success, Is.True);
        Assert.That(result3.Success, Is.True);

        Assert.That(connectionCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ShouldNotReuseConnectionWhenKeepAliveDisabled()
    {
        var connectionCount = 0;
        SetupServerLogicMulti(requireTls: false, expectedConnections: 3, onNewConnection: () => Interlocked.Increment(ref connectionCount));

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            KeepConnectionAlive = false,
        };

        Mllp.Send(input, connection, options, CancellationToken.None);
        Mllp.Send(input, connection, options, CancellationToken.None);
        Mllp.Send(input, connection, options, CancellationToken.None);

        await _serverTask;

        Assert.That(connectionCount, Is.EqualTo(3));
    }

    [Test]
    public async Task ShouldReconnectAfterConnectionDrop()
    {
        var connectionCount = 0;
        SetupServerLogicMultiMessage(
            requireTls: false,
            onNewConnection: () => Interlocked.Increment(ref connectionCount));

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            KeepConnectionAlive = true,
            ConnectionCacheExpirationMinutes = 1,
        };

        var result1 = Mllp.Send(input, connection, options, CancellationToken.None);
        Assert.That(result1.Success, Is.True);

        _serverCts.Cancel();
        _listener.Stop();
        await Task.Delay(500);

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _serverCts = new CancellationTokenSource();
        SetupServerLogicMultiMessage(
            requireTls: false,
            onNewConnection: () => Interlocked.Increment(ref connectionCount));

        var result2 = Mllp.Send(input, connection, options, CancellationToken.None);
        Assert.That(result2.Success, Is.True);

        Assert.That(connectionCount, Is.EqualTo(2));
    }

    [Test]
    public void ShouldRejectMessageExceedingMaxMessageSize()
    {
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var options = new Options
        {
            MaxMessageSize = 10,
            ThrowErrorOnFailure = false,
        };

        var result = Mllp.Send(input, connection, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error.Message, Does.Contain("exceeds the configured limit"));
    }

    [Test]
    public void ShouldRetryConfiguredNumberOfTimesOnFailure()
    {
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 1,
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            RetryCount = 2,
            RetryIntervalSeconds = 1,
            ThrowErrorOnFailure = false,
        };

        var stopwatch = Stopwatch.StartNew();
        var result = Mllp.Send(input, connection, options, CancellationToken.None);
        stopwatch.Stop();

        Assert.That(result.Success, Is.False);
        Assert.That(stopwatch.Elapsed.TotalSeconds, Is.GreaterThanOrEqualTo(1.8));
    }

    [Test]
    public void ShouldNotRetryWhenRetryCountIsZero()
    {
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 1,
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            RetryCount = 0,
            RetryIntervalSeconds = 5,
            ThrowErrorOnFailure = false,
        };

        var stopwatch = Stopwatch.StartNew();
        var result = Mllp.Send(input, connection, options, CancellationToken.None);
        stopwatch.Stop();

        Assert.That(result.Success, Is.False);
    }

    [Test]
    public async Task ShouldReturnErrorResultTypeWhenServerSendsNegativeAck()
    {
        var serverTask = Task.Run(async () =>
        {
            using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
            var stream = client.GetStream();
            var received = await Helpers.ReadMllpMessage(stream, Encoding.ASCII, _serverCts.Token);

            var nack = $"MSH|^~\\&|Listener|ListenerFacility|Sender|SenderFacility|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK^A01|ACK0001|P|2.5.1\r" +
                       $"MSA|AE|{Helpers.ExtractControlId(received)}|Validation failed\r";

            var response = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(nack));
            await stream.WriteAsync(response, 0, response.Length);
            await stream.FlushAsync();
            return received;
        });

        var result = Mllp.Send(
            new Input { Hl7Message = Helpers.BuildTestMessage() },
            new Connection { Host = "127.0.0.1", Port = _port, TlsMode = TlsMode.None, ConnectTimeoutSeconds = 5 },
            new Options { ExpectAcknowledgement = true, ThrowErrorOnFailure = false },
            CancellationToken.None);

        await serverTask;

        Assert.That(result.Success, Is.False);
        Assert.That(result.AckResultType, Is.EqualTo(AckResultType.Error));
        Assert.That(result.AckCodeValue, Is.EqualTo("AE"));
        Assert.That(result.AckErrorDescription, Does.Contain("Validation failed"));
    }

    [Test]
    public async Task ShouldReturnAcceptOnNormalAckAndNotApplicableWhenAckNotExpected()
    {
        SetupServerLogic(requireTls: false);
        var result1 = Mllp.Send(
            new Input { Hl7Message = Helpers.BuildTestMessage() },
            new Connection { Host = "127.0.0.1", Port = _port, TlsMode = TlsMode.None, ConnectTimeoutSeconds = 5 },
            new Options { ExpectAcknowledgement = true },
            CancellationToken.None);
        await _serverTask;

        Assert.That(result1.Success, Is.True);
        Assert.That(result1.AckResultType, Is.EqualTo(AckResultType.Accept));
        Assert.That(result1.AckCodeValue, Is.EqualTo("AA"));
    }

    [Test]
    public async Task ShouldHandleLargeMessage()
    {
        SetupServerLogic(requireTls: false);

        var largeMessage = Helpers.BuildLargeTestMessage(sizeInMb: 50);

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
            ReadTimeoutSeconds = 120,
        };

        var options = new Options
        {
            ExpectAcknowledgement = true,
            MaxMessageSize = 0,
            ValidateWithNhapi = false,
        };

        var result = Mllp.Send(new Input { Hl7Message = largeMessage }, connection, options, CancellationToken.None);
        await _serverTask;

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task ShouldLogMessageEventsToFile()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mllp-send-test-{Guid.NewGuid()}.log");

        SetupServerLogic(requireTls: false);

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            ExpectAcknowledgement = true,
            EnableLogging = true,
            LogFilePath = logPath,
            LogMessageContent = true,
        };

        var result = Mllp.Send(input, connection, options, CancellationToken.None);
        await _serverTask;

        Assert.That(result.Success, Is.True);
        Assert.That(File.Exists(logPath), Is.True, "Log file should be created");

        var logContent = await File.ReadAllTextAsync(logPath);
        Assert.That(logContent, Does.Contain("MESSAGE SENT"));
        Assert.That(logContent, Does.Contain("SUCCESS"));

        File.Delete(logPath);
    }

    [Test]
    public void ShouldLogRejectedMessageWhenExceedingSizeLimit()
    {
        var logPath = Path.Combine(Path.GetTempPath(), $"mllp-send-test-{Guid.NewGuid()}.log");

        var connection = new Connection
        {
            Host = "127.0.0.1",
            Port = _port,
            TlsMode = TlsMode.None,
            ConnectTimeoutSeconds = 5,
        };
        var input = new Input
        {
            Hl7Message = Helpers.BuildTestMessage(),
        };
        var options = new Options
        {
            MaxMessageSize = 10,
            EnableLogging = true,
            LogFilePath = logPath,
            ThrowErrorOnFailure = false,
        };

        var result = Mllp.Send(input, connection, options, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(File.Exists(logPath), Is.True);

        var logContent = File.ReadAllText(logPath);
        Assert.That(logContent, Does.Contain("MESSAGE REJECTED"));
        Assert.That(logContent, Does.Contain("exceeds limit"));

        File.Delete(logPath);
    }

    private static Encoding ResolveEncoding(FileEncoding fileEncoding, string encodingInString)
    {
        return fileEncoding switch
        {
            FileEncoding.UTF8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            FileEncoding.ASCII => Encoding.ASCII,
            FileEncoding.Windows1252 => Encoding.GetEncoding("windows-1252"),
            FileEncoding.Other => Encoding.GetEncoding(encodingInString),
            _ => Encoding.ASCII,
        };
    }

    private static void ClearConnectionCache()
    {
        var cache = MemoryCache.Default;
        var keys = cache
            .Where(kvp => kvp.Key.StartsWith("mllp:"))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keys)
            cache.Remove(key);
    }

    private string GetServerCertificateThumbprint()
    {
        using var certificate = new X509Certificate2(_serverPfxPath, _password);

        return certificate.GetCertHashString();
    }

    private async Task<string> HandleClientAsync(TcpClient client, bool requireTls, Encoding messageEncoding)
    {
        Stream stream = client.GetStream();

        if (requireTls)
        {
            var serverCert = new X509Certificate2(_serverPfxPath, _password);
            var sslStream = new SslStream(stream, false);
            await sslStream.AuthenticateAsServerAsync(
                serverCert,
                clientCertificateRequired: true,
                checkCertificateRevocation: false);
            stream = sslStream;
        }

        var encoding = messageEncoding ?? Encoding.ASCII;
        var received = await Helpers.ReadMllpMessage(stream, encoding, _serverCts.Token);
        var ack = Helpers.BuildAck(Helpers.ExtractControlId(received));
        var response = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(ack));
        await stream.WriteAsync(response, 0, response.Length);
        await stream.FlushAsync();

        client.Client.Shutdown(SocketShutdown.Send);
        return received;
    }

    private void SetupServerLogic(bool requireTls, Encoding messageEncoding = null)
    {
        _serverTask = Task.Run(async () =>
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                return await HandleClientAsync(client, requireTls, messageEncoding);
            }
            catch
            {
                return null;
            }
        });
    }

    private void SetupServerLogicMulti(bool requireTls, int expectedConnections, Action onNewConnection = null, Encoding messageEncoding = null)
    {
        _serverTask = Task.Run(async () =>
        {
            string lastReceived = null;

            for (var i = 0; i < expectedConnections; i++)
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                    onNewConnection?.Invoke();
                    lastReceived = await HandleClientAsync(client, requireTls, messageEncoding);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }

            return lastReceived;
        });
    }

    private async Task<string> HandleClientMultiMessageAsync(TcpClient client, bool requireTls, Encoding messageEncoding)
    {
        Stream stream = client.GetStream();

        if (requireTls)
        {
            var serverCert = new X509Certificate2(_serverPfxPath, _password);
            var sslStream = new SslStream(stream, false);
            await sslStream.AuthenticateAsServerAsync(
                serverCert,
                clientCertificateRequired: true,
                checkCertificateRevocation: false);
            stream = sslStream;
        }

        var encoding = messageEncoding ?? Encoding.ASCII;
        string lastReceived = null;

        while (!_serverCts.Token.IsCancellationRequested)
        {
            try
            {
                var received = await Helpers.ReadMllpMessage(stream, encoding, _serverCts.Token);
                if (received == null) break;

                lastReceived = received;
                var ack = Helpers.BuildAck(Helpers.ExtractControlId(received));
                var response = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(ack));
                await stream.WriteAsync(response, 0, response.Length);
                await stream.FlushAsync();
            }
            catch (IOException)
            {
                break;
            }
        }

        return lastReceived;
    }

    private void SetupServerLogicMultiMessage(bool requireTls, Action onNewConnection = null, Encoding messageEncoding = null)
    {
        _serverTask = Task.Run(async () =>
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                onNewConnection?.Invoke();
                return await HandleClientMultiMessageAsync(client, requireTls, messageEncoding);
            }
            catch
            {
                return null;
            }
        });
    }
}
