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

namespace Frends.Mllp.Send.Tests
{
     /* * MTLS TESTING: These tests require a trusted certificate environment.
     * To run locally without installing certs on Windows, use Docker from the solution root:
     * 1. docker build -t mllp-tests -f Frends.Mllp.Send.Tests/Dockerfile .
     * 2. docker run --rm mllp-tests
     * Note: Visual Studio Test Explorer might fail mTLS checks due to Windows trust store limits.
     */
    [TestFixture]
    public class FunctionalTests
    {
        private string _clientPfxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/client.pfx");
        private string _serverPfxPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData/server.pfx");
        private string _password = "password";

        private TcpListener _listener;
        private int _port;
        private CancellationTokenSource _serverCts;
        private Task<string> _serverTask;

        [SetUp]
        public void StartServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
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

            var input = new Input { Hl7Message = Helpers.BuildTestMessage() };

            var result = Mllp.Send(input, connection, new Options { ExpectAcknowledgement = true }, CancellationToken.None);
            var receivedByServer = await _serverTask;

            Assert.That(result.Success, Is.True);
            Assert.That(result.Output, Does.Contain("MSA|AA"));
            Assert.That(receivedByServer, Is.Not.Null);
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

            var result = Mllp.Send(new Input { Hl7Message = Helpers.BuildTestMessage() }, connection, new Options { ExpectAcknowledgement = true }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(_serverTask.Result, Does.Contain("MSG00001"));
        }

        [Test]
        public void NoCertMtlsSendShouldFail()
        {
            SetupServerLogic(requireTls: true);

            var input = new Input { Hl7Message = Helpers.BuildTestMessage() };

            var connection = new Connection
            {
                Host = "127.0.0.1",
                Port = _port,
                TlsMode = TlsMode.Mtls,
                ClientCertPath = null,
                IgnoreServerCertificateErrors = true,
                ConnectTimeoutSeconds = 2,
            };

            var options = new Options { ExpectAcknowledgement = true };

            var ex = Assert.Throws<Exception>(() =>
            {
                Mllp.Send(input, connection, options, CancellationToken.None);
            });

            Assert.That(ex.Message, Is.EqualTo("mTLS is enabled but client certificate path is missing."));
        }

        [Test]
        public void MtlsSendValidationShouldThrowOnUntrusted()
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
                ConnectTimeoutSeconds = 5,
            };

            var input = new Input { Hl7Message = Helpers.BuildTestMessage() };
            var options = new Options { ExpectAcknowledgement = true };
            var ex = Assert.Throws<Exception>(() =>
            {
                Mllp.Send(input, connection, options, CancellationToken.None);
            });

            Assert.That(ex.Message, Does.Contain("remote certificate was rejected")
                    .Or.Contain("RemoteCertificateValidationCallback"));
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

            var options = new Options { ExpectAcknowledgement = true };
            var input = new Input { Hl7Message = Helpers.BuildTestMessage() };
            var result = Mllp.Send(input, connection, new Options { ExpectAcknowledgement = true }, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Output, Is.Not.Null);
        }

        private void SetupServerLogic(bool requireTls)
        {
            _serverTask = Task.Run(async () =>
            {
                try
                {
                    using var client = await _listener.AcceptTcpClientAsync(_serverCts.Token);
                    Stream stream = client.GetStream();

                    if (requireTls)
                    {
                        var serverCert = new X509Certificate2(_serverPfxPath, _password);
                        var sslStream = new SslStream(stream, false);
                        await sslStream.AuthenticateAsServerAsync(serverCert, clientCertificateRequired: true, checkCertificateRevocation: false);
                        stream = sslStream;
                    }

                    var received = await Helpers.ReadMllpMessage(stream, _serverCts.Token);
                    var ack = Helpers.BuildAck(Helpers.ExtractControlId(received));
                    var response = Encoding.ASCII.GetBytes(MLLP.CreateMLLPMessage(ack));

                    await stream.WriteAsync(response, 0, response.Length);
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
}
