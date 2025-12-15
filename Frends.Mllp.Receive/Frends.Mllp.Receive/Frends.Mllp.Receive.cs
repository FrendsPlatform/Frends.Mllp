using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Text;
using System.Threading;
using Frends.Mllp.Receive.Definitions;
using Frends.Mllp.Receive.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperSocket;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
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
    public static Result Receive(
        [PropertyTab] Input input,
        [PropertyTab] Connection connection,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidateParameters(input, connection, options);

            var messages = new ConcurrentBag<string>();
            var encoding = options.GetEncoding();

            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedTokenSource.CancelAfter(TimeSpan.FromSeconds(options.ListenDurationSeconds));

            using var host = BuildMllpHost(input, connection, options, encoding, messages);

            host.StartAsync(linkedTokenSource.Token).GetAwaiter().GetResult();

            WaitForShutdown(linkedTokenSource.Token);

            host.StopAsync().GetAwaiter().GetResult();

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
    }

    private static void ValidateParameters(Input input, Connection connection, Options options)
    {
        if (input.Port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(input.Port), "Port must be between 1 and 65535.");

        if (options.ListenDurationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.ListenDurationSeconds), "Listen duration must be greater than zero.");

        if (options.BufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.BufferSize), "Buffer size must be positive.");

        _ = options.GetEncoding();

        if (!string.IsNullOrWhiteSpace(connection.ListenAddress) && !IPAddress.TryParse(connection.ListenAddress, out _))
            throw new FormatException("Invalid ListenAddress. Provide a valid IP address or leave the field empty.");
    }

    private static IHost BuildMllpHost(
        Input input,
        Connection connection,
        Options options,
        Encoding encoding,
        ConcurrentBag<string> messages)
    {
        var listenIp = string.IsNullOrWhiteSpace(connection.ListenAddress) ? "Any" : connection.ListenAddress;
        var ackBytes = GetAckBytes(options, encoding);

        return SuperSocketHostBuilder.Create<MllpPackage, MllpPipelineFilter>()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton(encoding);
            })
            .UsePackageHandler(async (session, package) =>
            {
                messages.Add(package.Payload);

                if (!options.SendAcknowledgement)
                    return;

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
                opt.Name = "MllpServer";
                opt.ReceiveBufferSize = options.BufferSize;
                opt.Listeners = new List<ListenOptions>
                {
                    new ()
                    {
                        Ip = listenIp,
                        Port = input.Port,
                    },
                };
            })
            .Build();
    }

    private static byte[] GetAckBytes(Options options, Encoding encoding)
    {
        if (!options.SendAcknowledgement)
            return Array.Empty<byte>();

        var ackPayload = string.IsNullOrEmpty(options.AcknowledgementMessage) ? "ACK" : options.AcknowledgementMessage;
        var ackMessage = $"{StartBlock}{ackPayload}{EndBlock}{CarriageReturn}";
        return encoding.GetBytes(ackMessage);
    }

    private static void WaitForShutdown(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.WaitHandle.WaitOne();
        }
        catch (ObjectDisposedException)
        {
            // If the token source is already disposed, we are done waiting.
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
