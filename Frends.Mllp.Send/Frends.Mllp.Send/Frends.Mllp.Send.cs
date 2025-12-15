using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Frends.Mllp.Send.Definitions;
using Frends.Mllp.Send.Helpers;
using NHapi.Base.Parser;
using NHapiTools.Base.Net;
using NHapiTools.Base.Util;

namespace Frends.Mllp.Send;

/// <summary>
/// Task Class for Mllp operations.
/// </summary>
public static class Mllp
{
    /// <summary>
    /// Sends a single HL7 message via MLLP.
    /// [Documentation](https://tasks.frends.com/tasks/frends-tasks/Frends-Mllp-Send)
    /// </summary>
    /// <param name="input">Essential parameters.</param>
    /// <param name="connection">Connection parameters.</param>
    /// <param name="options">Additional parameters.</param>
    /// <param name="cancellationToken">A cancellation token provided by Frends Platform.</param>
    /// <returns>object { bool Success, string Output, object Error { string Message, Exception AdditionalInfo } }</returns>
    public static Result Send(
        [PropertyTab] Input input,
        [PropertyTab] Connection connection,
        [PropertyTab] Options options,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(input.Hl7Message))
                throw new ArgumentException("HL7 message cannot be empty.", nameof(input.Hl7Message));

            var parser = options.ValidateWithNhapi ? new PipeParser() : null;
            var message = PrepareMessage(input.Hl7Message, parser);
            var receiveTimeoutMs = (int)TimeSpan.FromSeconds(connection.ReadTimeoutSeconds).TotalMilliseconds;

            var acknowledgement = string.Empty;
            if (options.ExpectAcknowledgement)
            {
                using var client = new SimpleMLLPClient(connection.Host, connection.Port, Encoding.ASCII, receiveTimeoutMs);
                acknowledgement = client.SendHL7Message(message, connection.ReadTimeoutSeconds);
            }
            else
            {
                SendWithoutAcknowledgement(message, connection, cancellationToken);
            }

            return new Result
            {
                Success = true,
                Output = acknowledgement,
                Error = null,
            };
        }
        catch (Exception ex)
        {
            return ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure);
        }
    }

    private static string PrepareMessage(string hl7Message, PipeParser parser)
    {
        hl7Message = NormalizeLineEndings(hl7Message);

        if (parser is null)
            return EnsureEndsWithCarriageReturn(hl7Message);

        try
        {
            var parsed = parser.Parse(hl7Message);
            var encoded = parser.Encode(parsed);
            return EnsureEndsWithCarriageReturn(encoded);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("HL7 message is not valid according to NHapi.", ex);
        }
    }

    private static string NormalizeLineEndings(string message)
    {
        return message.Replace("\r\n", "\r").Replace("\n", "\r");
    }

    private static string EnsureEndsWithCarriageReturn(string message)
    {
        if (message.EndsWith("\r", StringComparison.Ordinal))
            return message;

        return $"{message}\r";
    }

    private static void SendWithoutAcknowledgement(string message, Connection connection, CancellationToken cancellationToken)
    {
        var framed = MLLP.CreateMLLPMessage(message);
        var payload = Encoding.ASCII.GetBytes(framed);

        using var tcpClient = new TcpClient();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(connection.ConnectTimeoutSeconds));
        tcpClient.ConnectAsync(connection.Host, connection.Port, cts.Token).GetAwaiter().GetResult();
        tcpClient.SendTimeout = (int)TimeSpan.FromSeconds(connection.ConnectTimeoutSeconds).TotalMilliseconds;

        using var stream = tcpClient.GetStream();
        stream.Write(payload, 0, payload.Length);
        stream.Flush();
    }
}