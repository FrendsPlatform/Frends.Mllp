using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
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
        _listener = new TcpListener(
            IPAddress.Loopback,
            0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _serverCts = new CancellationTokenSource();
    }

    [TearDown]
    public void StopServer()
    {
        _serverCts.Cancel();
        _listener.Stop();
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

    private string GetServerCertificateThumbprint()
    {
        using var certificate = new X509Certificate2(_serverPfxPath, _password);

        return certificate.GetCertHashString();
    }

    private void SetupServerLogic(bool requireTls, Encoding messageEncoding = null)
    {
        _serverTask = Task.Run(async () =>
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                Stream stream = client.GetStream();

                if (requireTls)
                {
                    var serverCert = new X509Certificate2(
                        _serverPfxPath,
                        _password);
                    var sslStream = new SslStream(
                        stream,
                        false);
                    await sslStream.AuthenticateAsServerAsync(
                        serverCert,
                        clientCertificateRequired: true,
                        checkCertificateRevocation: false);
                    stream = sslStream;
                }

                var encoding = messageEncoding ?? Encoding.ASCII;
                var received = await Helpers.ReadMllpMessage(
                    stream,
                    encoding,
                    _serverCts.Token);
                var ack = Helpers.BuildAck(Helpers.ExtractControlId(received));
                var response = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(ack));

                await stream.WriteAsync(
                    response,
                    0,
                    response.Length);
                await stream.FlushAsync();

                return received;
            }
            catch
            {
                return null;
            }
        });
    }
}
