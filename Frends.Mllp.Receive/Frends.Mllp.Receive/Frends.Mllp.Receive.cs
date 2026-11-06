using System;
using System.Buffers;
using System.ComponentModel;
using System.IO;
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
using SuperSocket.Server.Abstractions.Session;
using SuperSocket.Server.Host;

namespace Frends.Mllp.Receive;

/// <summary>
/// Task Class for Mllp operations.
/// </summary>
public static class Mllp
{
    /// <summary>
    /// Starts an MLLP server that collects incoming HL7 messages for the configured duration.
    /// [Documentation](https://tasks.frends.com/tasks/frends-tasks/Frends-Mllp-Receive)
    /// </summary>
    /// <param name="input">Essential parameters.</param>
    /// <param name="connection">Connection parameters.</param>
    /// <param name="options">Additional parameters.</param>
    /// <param name="cancellationToken">A cancellation token provided by Frends Platform.</param>
    /// <returns>object { bool Success, string[] Output, string[] Warnings, object Error { string Message, Exception AdditionalInfo } }</returns>
    public static Task<Result> Receive(
        [PropertyTab] Input input,
        [PropertyTab] Connection connection,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        return ReceiveCore(input, connection, options, new MessageCollector(), cancellationToken);
    }

    /// <summary>
    /// Implementation of <see cref="Receive"/> with an injectable collector, so tests can exercise a closed
    /// receive window deterministically.
    /// </summary>
    /// <param name="input">Essential parameters.</param>
    /// <param name="connection">Connection parameters.</param>
    /// <param name="options">Additional parameters.</param>
    /// <param name="collector">Collects the received messages; closed when the result is built.</param>
    /// <param name="cancellationToken">A cancellation token provided by Frends Platform.</param>
    /// <returns>The task result.</returns>
    internal static async Task<Result> ReceiveCore(
        Input input,
        Connection connection,
        Options options,
        MessageCollector collector,
        CancellationToken cancellationToken)
    {
        var diagnostics = new Diagnostics();
        X509Certificate2 serverCert = null;
        MessageLogger logger = null;
        IHost host = null;
        var started = false;
        Exception failure = null;
        var received = Array.Empty<string>();
        var warnings = Array.Empty<string>();

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidationHandler.Run(input, connection, options);

            var encoding = GetEncoding(connection);

            logger = new MessageLogger(options.EnableLogging, options.LogFilePath, options.LogMessageContent, diagnostics);
            var connectionTracker = new ConnectionTracker(options.MaxConcurrentConnections);

            if (connection.TlsMode == TlsMode.Mtls)
            {
                if (string.IsNullOrEmpty(connection.ServerCertPath))
                    throw new ArgumentException("Server certificate path is required for Mtls mode.");
                serverCert = new X509Certificate2(connection.ServerCertPath, connection.ServerCertPassword);
            }

            host = BuildMllpHost(input, connection, options, encoding, collector, logger, connectionTracker, serverCert);

            await host.StartAsync(cancellationToken);
            started = true;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(connection.ListenDurationSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            // Runs on every path, so the listener is always shut down. Nothing here can throw: closing the collector
            // only takes a lock, and the helpers catch everything and record a warning instead.
            // Closing the collector ends the receive window: no message can be added (and so none acknowledged)
            // from here on, even if the listener is slow or fails to stop.
            received = collector.Close();

            // A host that failed to start is in a failed state and cannot be stopped; only dispose it.
            if (started)
                await StopHostQuietly(host, diagnostics);

            DisposeQuietly(host, "MLLP host", diagnostics);
            DisposeQuietly(logger, "message logger", diagnostics);
            DisposeQuietly(serverCert, "server certificate", diagnostics);
            warnings = diagnostics.ToArray();
        }

        if (failure is null)
            return new Result { Success = true, Output = received, Warnings = warnings, Error = null };

        // Every failure happens before the listener has accepted a connection (once started, only the listen delay
        // remains: its cancellation is handled and its duration is range-validated), so there are no received
        // messages to report here.
        return ErrorHandler.Handle(failure, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure, warnings);
    }

    private static async Task StopHostQuietly(IHost host, Diagnostics diagnostics)
    {
        var stopTimeout = TimeSpan.FromSeconds(10);
        try
        {
            // Own timeout instead of the task's token: it may already be cancelled, which would abort the stop.
            using var stopCts = new CancellationTokenSource(stopTimeout);
            await host.StopAsync(stopCts.Token);
        }
        catch (OperationCanceledException)
        {
            diagnostics.Warn($"The MLLP host did not stop within {stopTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex)
        {
            // The collector is already closed, so a listener that fails to stop cannot acknowledge messages.
            diagnostics.Warn($"Failed to stop the MLLP host cleanly: {ex.Message}");
        }
    }

    private static void DisposeQuietly(IDisposable disposable, string name, Diagnostics diagnostics)
    {
        try
        {
            disposable?.Dispose();
        }
        catch (Exception ex)
        {
            diagnostics.Warn($"Failed to dispose {name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Refuses a message that arrived after the receive window closed: no ACK is sent and the connection is closed,
    /// so the sender can retry. Never throws, so it cannot fall into the NACK path of the package handler.
    /// </summary>
    private static async ValueTask RefuseLateMessage(IAppSession session, MllpPackage package, MessageLogger logger)
    {
        logger.LogMessageRejected(session.SessionID, "Receive window closed", package.PayloadSize);

        if (package.IsFilePath)
        {
            try
            {
                File.Delete(package.Payload);
            }
            catch
            {
                // Best effort: the file was never reported in the result.
            }
        }

        try
        {
            // LocalClosing: a deliberate close, so the Closed handler does not log it as a dropped connection.
            await session.CloseAsync(SuperSocket.Connection.CloseReason.LocalClosing);
        }
        catch
        {
            // The session may already be closing because the server is shutting down.
        }
    }

    private static Encoding GetEncoding(Connection connection)
    {
        return connection.Encoding switch
        {
            FileEncoding.UTF8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            FileEncoding.Default => Encoding.Default,
            FileEncoding.ASCII => Encoding.ASCII,
            FileEncoding.Unicode => Encoding.Unicode,
            FileEncoding.Windows1252 => GetExtendedEncoding("windows-1252"),
            FileEncoding.Other => GetExtendedEncoding(connection.EncodingInString),
            _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        };
    }

    private static Encoding GetExtendedEncoding(string name)
    {
        CodePagesEncodingProviderRegistrar.EnsureRegistered();

        return Encoding.GetEncoding(name);
    }

    private static IHost BuildMllpHost(
        Input input,
        Connection connection,
        Options options,
        Encoding encoding,
        MessageCollector collector,
        MessageLogger logger,
        ConnectionTracker connectionTracker,
        X509Certificate2 serverCert)
    {
        var listenIp = string.IsNullOrWhiteSpace(input.ListenAddress) ? "Any" : input.ListenAddress;

        return SuperSocketHostBuilder.Create<MllpPackage, MllpPipelineFilter>()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(encoding);
                services.AddSingleton(new MllpFramingBytes
                {
                    StartBlock = options.StartBlockByte,
                    EndBlock = options.EndBlockByte,
                    CarriageReturn = options.CarriageReturnByte,
                    CarriageReturnRequired = options.CarriageReturnRequired,
                });
                services.AddSingleton(options);
                services.AddSingleton(logger);
                services.AddSingleton(connectionTracker);
            })
            .UseSessionHandler(async (session) =>
            {
                var sessionId = session.SessionID;

                // Check connection limit
                if (!connectionTracker.TryIncrementConnection())
                {
                    logger.LogConnectionRejected(sessionId, $"Max concurrent connections limit ({options.MaxConcurrentConnections}) reached");
                    await session.CloseAsync(SuperSocket.Connection.CloseReason.ServerShutdown);
                    return;
                }

                // Handle connection closed
                session.Closed += (s, e) =>
                {
                    connectionTracker.DecrementConnection();
                    if (e.Reason != SuperSocket.Connection.CloseReason.LocalClosing)
                    {
                        logger.LogConnectionDropped(sessionId, e.Reason.ToString());
                    }

                    return ValueTask.CompletedTask;
                };

                return;
            })
            .UsePackageHandler(async (session, package) =>
            {
                var sessionId = session.SessionID;

                // Handle framing errors
                if (package.HasFramingError)
                {
                    logger.LogFramingError(sessionId, package.FramingError);
                    return;
                }

                // Check message size limit
                if (options.MaxMessageSize > 0 && package.PayloadSize > options.MaxMessageSize)
                {
                    logger.LogMessageRejected(sessionId, $"Message size ({package.PayloadSize} bytes) exceeds limit ({options.MaxMessageSize} bytes)", package.PayloadSize);

                    if (connection.SendAcknowledgement)
                    {
                        var nackBytes = BuildNegativeAcknowledgement(package.Payload, options, encoding, "Message too large");
                        try
                        {
                            await session.SendAsync(nackBytes);
                        }
                        catch
                        {
                            // Ignore send failures
                        }
                    }

                    return;
                }

                try
                {
                    logger.LogMessageReceived(package.Payload, sessionId);

                    // Everything that can fail (reading the message file, building the ACK) happens before the
                    // message is recorded. A failure therefore sends a NACK for a message that is not in the
                    // result, and nothing after the recording below can throw into the NACK path.
                    var ackBytes = connection.SendAcknowledgement
                        ? BuildAcknowledgementBytes(
                            package.IsFilePath ? ReadMshFromFile(package.Payload, encoding) : package.Payload,
                            connection,
                            options,
                            encoding)
                        : null;

                    // Record the message before acknowledging it. If the receive window has closed the message is
                    // refused and not acknowledged, so the sender can retry. An ACK therefore always implies that
                    // the message is part of the result.
                    if (!collector.TryAdd(package.Payload))
                    {
                        await RefuseLateMessage(session, package, logger);
                        return;
                    }

                    if (ackBytes is null)
                    {
                        logger.LogMessageSuccess(package.Payload, sessionId, false, "ACK disabled in configuration");
                        return;
                    }

                    if (ackBytes.Length == 0)
                    {
                        logger.LogMessageSuccess(package.Payload, sessionId, false, "ACK could not be built (invalid message format)");
                        return;
                    }

                    try
                    {
                        await session.SendAsync(ackBytes);
                        logger.LogMessageSuccess(package.Payload, sessionId, true, null);
                    }
                    catch (Exception ex)
                    {
                        logger.LogAcknowledgementFailure(sessionId, ex);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogMessageFailure(package.Payload, sessionId, ex);
                    if (connection.SendAcknowledgement)
                    {
                        // The failure may have been reading this very file; fall back to a generic NACK then.
                        string mshForNack;
                        try
                        {
                            mshForNack = package.IsFilePath
                                ? ReadMshFromFile(package.Payload, encoding)
                                : package.Payload;
                        }
                        catch
                        {
                            mshForNack = null;
                        }

                        var nackBytes = BuildNegativeAcknowledgement(mshForNack, options, encoding, ex.Message);
                        try
                        {
                            await session.SendAsync(nackBytes);
                        }
                        catch
                        {
                            logger.LogAcknowledgementFailure(sessionId, ex);
                        }
                    }
                }
            })
            .ConfigureSuperSocket(opt =>
            {
                opt.Name = "FrendsMllpServer";

                opt.ReceiveBufferSize = connection.BufferSize;

                opt.MaxPackageLength = options.MaxMessageSize > 0
                    ? options.MaxMessageSize + 1024
                    : 100 * 1024 * 1024;

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
                        RemoteCertificateValidationCallback = (_, cert, _, errors) =>
                        {
                            if (connection.IgnoreClientCertificateErrors) return true;
                            if (connection.ClientCertificateThumbprints.Length <= 0)
                                return errors == SslPolicyErrors.None;

                            if (cert is null) return false;
                            var thumbprint = Normalize(cert.GetCertHashString());

                            return errors != SslPolicyErrors.RemoteCertificateNotAvailable && Array.Exists(
                                connection.ClientCertificateThumbprints,
                                expected => !string.IsNullOrWhiteSpace(expected) &&
                                            Normalize(expected).Equals(thumbprint, StringComparison.OrdinalIgnoreCase));
                        },
                    };
                }

                opt.Listeners = [listener];
            })
            .Build();
    }

    private static byte[] BuildAcknowledgementBytes(
        string message,
        Connection connection,
        Options options,
        Encoding encoding)
    {
        if (options.AcknowledgementFormat == AcknowledgementFormat.ControlByte)
        {
            return BuildControlByteAck(options, encoding);
        }

        var ackPayload = BuildAcknowledgement(message, connection);
        if (string.IsNullOrEmpty(ackPayload))
        {
            return [];
        }

        return WrapWithMllpFraming(ackPayload, options, encoding);
    }

    private static byte[] BuildControlByteAck(Options options, Encoding encoding)
    {
        if (options.CarriageReturnRequired)
        {
            return
            [
                options.StartBlockByte,
                options.AcknowledgementByte,
                options.EndBlockByte,
                options.CarriageReturnByte,
            ];
        }
        else
        {
            return
            [
                options.StartBlockByte,
                options.AcknowledgementByte,
                options.EndBlockByte,
            ];
        }
    }

    private static byte[] BuildNegativeAcknowledgement(
    string message,
    Options options,
    Encoding encoding,
    string reason)
    {
        if (options.AcknowledgementFormat == AcknowledgementFormat.ControlByte)
        {
            if (options.CarriageReturnRequired)
                return [options.StartBlockByte, 0x15, options.EndBlockByte, options.CarriageReturnByte];
            else
                return [options.StartBlockByte, 0x15, options.EndBlockByte];
        }

        try
        {
            var mshEnd = message?.IndexOf('\r') ?? -1;
            var mshOnly = mshEnd > 0 ? message[..mshEnd] : message;

            var parser = new PipeParser();
            var parsed = parser.Parse(mshOnly);

            if (parsed is not IMessage inbound)
                return BuildFallbackNack(options, encoding, reason);

            var inboundTerser = new Terser(inbound);

            var nack = inbound.GenerateAck(AckTypes.AE, inboundTerser.Get("/MSH-5"), inboundTerser.Get("/MSH-6"), string.Empty);
            var nackTerser = new Terser(nack);

            if (!string.IsNullOrEmpty(reason))
                nackTerser.Set("/MSA-3", reason);

            return WrapWithMllpFraming(parser.Encode(nack), options, encoding);
        }
        catch
        {
            return BuildFallbackNack(options, encoding, reason);
        }
    }

    private static byte[] BuildFallbackNack(Options options, Encoding encoding, string reason)
    {
        var nackMessage = $"MSH|^~\\&|||||||ACK||P|2.5\rMSA|AE||{reason}";
        return WrapWithMllpFraming(nackMessage, options, encoding);
    }

    private static byte[] WrapWithMllpFraming(string payload, Options options, Encoding encoding)
    {
        string framedMessage;
        if (options.CarriageReturnRequired)
        {
            framedMessage = $"{(char)options.StartBlockByte}{payload}{(char)options.EndBlockByte}{(char)options.CarriageReturnByte}";
        }
        else
        {
            framedMessage = $"{(char)options.StartBlockByte}{payload}{(char)options.EndBlockByte}";
        }

        return encoding.GetBytes(framedMessage);
    }

    private static string Normalize(string value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Replace(":", string.Empty).Replace("-", string.Empty).ToUpperInvariant();

    private static string BuildAcknowledgement(string message, Connection connection)
    {
        if (string.IsNullOrWhiteSpace(message))
            return string.Empty;

        try
        {
            // HL7 segments are separated by \r — extract only the MSH segment (first line)
            var firstSegmentEnd = message.IndexOfAny(['\r', '\n']);
            var mshSegment = firstSegmentEnd > 0 ? message[..firstSegmentEnd] : message;

            var parser = new PipeParser();
            var parsed = parser.Parse(mshSegment);

            if (parsed is not IMessage inbound)
                return string.Empty;

            var inboundTerser = new Terser(inbound);
            var ackType = (AckTypes)connection.AcknowledgementType;

            var ackApp = !string.IsNullOrEmpty(connection.AckSenderApplication)
                ? connection.AckSenderApplication
                : inboundTerser.Get("/MSH-5");

            var ackFacility = inboundTerser.Get("/MSH-6");

            var ack = inbound.GenerateAck(ackType, ackApp, ackFacility, string.Empty);
            var ackTerser = new Terser(ack);

            if (!string.IsNullOrEmpty(connection.AckReceiverApplication))
                ackTerser.Set("/MSH-5", connection.AckReceiverApplication);

            if (!string.IsNullOrEmpty(connection.AckHl7Version))
                ackTerser.Set("/MSH-12", connection.AckHl7Version);

            return parser.Encode(ack);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadMshFromFile(string filePath, Encoding encoding)
    {
        using var file = File.OpenRead(filePath);
        var buffer = new byte[1024];
        var read = file.Read(buffer, 0, buffer.Length);
        var text = encoding.GetString(buffer, 0, read);
        var mshEnd = text.IndexOf('\r');
        return mshEnd > 0 ? text[..mshEnd] : text;
    }

    /// <summary>
    /// Represents a parsed MLLP payload.
    /// </summary>
    /// <example>MSH|^~\&amp;|HIS|RIH|...</example>
    private sealed class MllpFramingBytes
    {
        /// <summary>
        /// Start block byte.
        /// </summary>
        /// <example>11</example>
        public byte StartBlock { get; init; }

        /// <summary>
        /// End block byte.
        /// </summary>
        /// <example>28</example>
        public byte EndBlock { get; init; }

        /// <summary>
        /// Carriage return byte.
        /// </summary>
        /// <example>13</example>
        public byte CarriageReturn { get; init; }

        /// <summary>
        /// Indicates whether a carriage return byte is required.
        /// </summary>
        /// <example>true</example>
        public bool CarriageReturnRequired { get; init; }
    }

    /// <summary>
    /// Represents a parsed MLLP payload.
    /// </summary>
    /// <example>MSH|^~\&amp;|HIS|RIH|...</example>
    private sealed class MllpPackage
    {
        public MllpPackage(string payload, int payloadSize, bool isFilePath = false)
        {
            Payload = payload;
            PayloadSize = payloadSize;
            IsFilePath = isFilePath;
            HasFramingError = false;
            FramingError = null;
        }

        public MllpPackage(string framingError)
        {
            Payload = null;
            PayloadSize = 0;
            IsFilePath = false;
            HasFramingError = true;
            FramingError = framingError;
        }

        /// <summary>
        /// Raw message content without MLLP framing characters.
        /// Null if HasFramingError is true.
        /// </summary>
        /// <example>MSH|^~\&amp;|HIS|RIH|...</example>
        public string Payload { get; }

        /// <summary>
        /// Size of the payload in bytes before encoding conversion.
        /// Zero if HasFramingError is true.
        /// </summary>
        /// <example>256</example>
        public int PayloadSize { get; }

        /// <summary>
        /// Indicates whether the payload is stored in a file.
        /// </summary>
        /// <example>false</example>
        public bool IsFilePath { get; }

        /// <summary>
        /// Indicates whether a framing error occurred during message parsing.
        /// When true, Payload is null and FramingError contains error details.
        /// </summary>
        /// <example>false</example>
        public bool HasFramingError { get; }

        /// <summary>
        /// Description of the framing error that occurred.
        /// Null if HasFramingError is false. Contains details about missing start/end blocks or invalid carriage return.
        /// </summary>
        /// <example>Missing start block (expected 11, got 65)</example>
        public string FramingError { get; }
    }

    /// <summary>
    /// MLLP pipeline filter implementation with framing validation.
    /// </summary>
    private sealed class MllpPipelineFilter : PipelineFilterBase<MllpPackage>
    {
        private readonly Encoding encoding;
        private readonly MllpFramingBytes framing;
        private readonly Options options;

        public MllpPipelineFilter(IServiceProvider serviceProvider)
        {
            encoding = serviceProvider.GetRequiredService<Encoding>();
            framing = serviceProvider.GetRequiredService<MllpFramingBytes>();
            options = serviceProvider.GetRequiredService<Options>();
        }

        /// <summary>
        /// Parses the incoming byte sequence according to MLLP framing rules.
        /// </summary>
        public override MllpPackage Filter(ref SequenceReader<byte> reader)
        {
            if (!reader.TryRead(out byte firstByte))
                return null;

            if (firstByte != framing.StartBlock)
                return new MllpPackage($"Missing start block (expected {framing.StartBlock}, got {firstByte})");

            if (!reader.TryReadTo(out ReadOnlySequence<byte> payload, framing.EndBlock))
            {
                reader.Rewind(reader.Consumed);
                return null;
            }

            if (framing.CarriageReturnRequired)
            {
                if (!reader.TryRead(out byte crByte))
                {
                    reader.Rewind(reader.Consumed);
                    return null;
                }

                if (crByte != framing.CarriageReturn)
                    return new MllpPackage($"Missing or invalid carriage return (expected {framing.CarriageReturn}, got {crByte})");
            }

            var payloadSize = (int)payload.Length;

            if (options.WriteMessagesToFile)
            {
                if (options.MaxMessageSize > 0 && payloadSize > options.MaxMessageSize)
                    return new MllpPackage($"Message size {payloadSize} exceeds maximum allowed size {options.MaxMessageSize}");

                var dir = string.IsNullOrWhiteSpace(options.MessageOutputDirectory)
                    ? Path.GetTempPath()
                    : options.MessageOutputDirectory;

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var filePath = Path.Combine(dir, $"mllp-{Guid.NewGuid()}.hl7");

                using var file = File.OpenWrite(filePath);
                foreach (var segment in payload)
                    file.Write(segment.Span);

                return new MllpPackage(filePath, payloadSize, isFilePath: true);
            }

            var payloadSpan = payload.IsSingleSegment ? payload.FirstSpan : payload.ToArray();
            var message = encoding.GetString(payloadSpan);
            return new MllpPackage(message, payloadSize);
        }
    }
}
