using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using Frends.Mllp.Receive.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Util;
using NHapiTools.Base;
using NHapiTools.Base.Util;
using SuperSocket.ProtoBase;
using SuperSocket.Server.Abstractions;
using SuperSocket.Server.Host;

namespace Frends.Mllp.Receive;

/// <summary>
/// Task Class for Mllp operations.
/// </summary>
public static class Mllp
{
    private const char StartBlock = '\u000b';
    private const char EndBlock = '\u001c';
    private const char CarriageReturn = '\r';
    private const byte StartBlockByte = 0x0b;
    private const byte EndBlockByte = 0x1c;
    private const byte CarriageReturnByte = 0x0d;

    /// <summary>
    /// Starts an MLLP server that collects incoming HL7 messages for the configured duration.
    /// [Documentation](https://tasks.frends.com/tasks/frends-tasks/Frends-Mllp-Receive)
    /// </summary>
    /// <param name="input">Essential parameters.</param>
    /// <param name="connection">Connection parameters.</param>
    /// <param name="options">Additional parameters.</param>
    /// <param name="cancellationToken">A cancellation token provided by Frends Platform.</param>
    /// <returns>object { bool Success, string[] Output, object Error { string Message, Exception AdditionalInfo } }</returns>
    public static async Task<Result> Receive(
        [PropertyTab] Input input,
        [PropertyTab] Connection connection,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        X509Certificate2 serverCert = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateParameters(input, connection);

            var messages = new ConcurrentQueue<string>();
            var encoding = connection.GetEncoding();

            if (connection.TlsMode == TlsMode.Mtls)
            {
                if (string.IsNullOrEmpty(connection.ServerCertPath))
                    throw new ArgumentException("Server certificate path is required for Mtls mode.");
                serverCert = new X509Certificate2(connection.ServerCertPath, connection.ServerCertPassword);
            }

            using var host = BuildMllpHost(input, connection, encoding, messages, serverCert);

            await host.StartAsync(cancellationToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(connection.ListenDurationSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }

            try
            {
                await host.StopAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
            }

            return new Result
            {
                Success = true,
                Output = messages.ToArray(),
                Error = null,
            };
        }
        catch (Exception ex)
        {
            return ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure);
        }
        finally
        {
            serverCert?.Dispose();
        }
    }

    private static void ValidateParameters(Input input, Connection connection)
    {
        if (input.Port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(input), "Port must be between 1 and 65535.");

        if (connection.ListenDurationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(connection), "Listen duration must be greater than zero.");

        if (connection.BufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(connection), "Buffer size must be positive.");

        _ = connection.GetEncoding();

        if (!string.IsNullOrWhiteSpace(input.ListenAddress) && !IPAddress.TryParse(input.ListenAddress, out _))
            throw new FormatException("Invalid ListenAddress. Provide a valid IP address or leave the field empty.");
    }

    private static IHost BuildMllpHost(
        Input input,
        Connection connection,
        Encoding encoding,
        ConcurrentQueue<string> messages,
        X509Certificate2 serverCert)
    {
        var listenIp = string.IsNullOrWhiteSpace(input.ListenAddress) ? "Any" : input.ListenAddress;

        return SuperSocketHostBuilder.Create<MllpPackage, MllpPipelineFilter>()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(encoding);
            })
            .UsePackageHandler(async (session, package) =>
            {
                messages.Enqueue(package.Payload);

                if (!connection.SendAcknowledgement)
                    return;

                var ackPayload = BuildAcknowledgement(package.Payload, connection);

                if (string.IsNullOrEmpty(ackPayload))
                {
                    return;
                }

                var ackMessage = $"{StartBlock}{ackPayload}{EndBlock}{CarriageReturn}";

                var ackBytes = encoding.GetBytes(ackMessage);

                try
                {
                    await session.SendAsync(ackBytes);
                }
                catch
                {
                    // Client may close the connection before the ACK is sent. Ignore send failures.
                }
            })
            .ConfigureSuperSocket(opt =>
            {
                opt.Name = "FrendsMllpServer";
                opt.ReceiveBufferSize = connection.BufferSize;
                var listener = new ListenOptions
                {
                    Ip = listenIp,
                    Port = input.Port,
                };

                if (connection.TlsMode == TlsMode.Mtls)
                {
                    listener.AuthenticationOptions = new ServerAuthenticationOptions
                    {
                        ServerCertificate = serverCert,
                        ClientCertificateRequired = true,
                        EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                        CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                        RemoteCertificateValidationCallback = (sender, cert, chain, errors) =>
                        {
                            if (connection.IgnoreClientCertificateErrors)
                                return true;

                            return errors == SslPolicyErrors.None;
                        },
                    };
                }

                opt.Listeners = new List<ListenOptions> { listener };
            })
        .Build();
    }

    private static string BuildAcknowledgement(string message, Connection connection)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        try
        {
            var parser = new PipeParser();
            var parsed = parser.Parse(message);
            if (parsed is not IMessage inbound)
                return string.Empty;

            var inboundTerser = new Terser(inbound);
            var ackType = Enum.TryParse(connection.AcknowledgementMessage, true, out AckTypes parsedAck)
                ? parsedAck
                : AckTypes.AA;
            var ackApp = inboundTerser.Get("/MSH-5");
            var ackFacility = inboundTerser.Get("/MSH-6");
            var ack = inbound.GenerateAck(ackType, ackApp, ackFacility, string.Empty);

            return parser.Encode(ack);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Represents a parsed MLLP payload.
    /// </summary>
    /// <example>MSH|^~\&amp;|HIS|RIH|...</example>
    private sealed class MllpPackage
    {
        public MllpPackage(string payload) => Payload = payload;

        /// <summary>
        /// Raw message content without MLLP framing.
        /// </summary>
        /// <example>MSH|^~\&amp;|HIS|RIH|...</example>
        public string Payload { get; }
    }

    /// <summary>
    /// Pipeline filter that extracts MLLP-framed messages.
    /// </summary>
    private sealed class MllpPipelineFilter : BeginEndMarkPipelineFilter<MllpPackage>
    {
        private readonly Encoding encoding;

        public MllpPipelineFilter(Encoding encoding)
            : base(new[] { StartBlockByte }, new[] { EndBlockByte, CarriageReturnByte })
        {
            this.encoding = encoding;
        }

        protected override MllpPackage DecodePackage(ref ReadOnlySequence<byte> buffer)
        {
            var payload = buffer.GetString(encoding);
            return new MllpPackage(payload);
        }
    }
}