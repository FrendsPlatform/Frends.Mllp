using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
                throw new ArgumentException("HL7 message cannot be empty.", nameof(input));

            var parser = options.ValidateWithNhapi ? new PipeParser() : null;
            var message = PrepareMessage(input.Hl7Message, parser);
            var connectTimeoutMs = (int)TimeSpan.FromSeconds(connection.ConnectTimeoutSeconds).TotalMilliseconds;
            var receiveTimeoutMs = (int)TimeSpan.FromSeconds(connection.ReadTimeoutSeconds).TotalMilliseconds;

            var acknowledgement = string.Empty;
            using (var wrapper = new MtlsMllpWrapper(connection.Host, connection.Port, Encoding.ASCII, connectTimeoutMs))
            {
                if (connection.TlsMode == TlsMode.Mtls)
                {
                    if (string.IsNullOrEmpty(connection.ClientCertPath))
                        throw new Exception("mTLS is enabled but client certificate path is missing.");

                    using var clientCert = new X509Certificate2(connection.ClientCertPath, connection.ClientCertPassword);

                    wrapper.EnableMtls(clientCert, connection.Host, connection.IgnoreServerCertificateErrors);
                }

                if (options.ExpectAcknowledgement)
                {
                    acknowledgement = wrapper.Send(message, receiveTimeoutMs);
                }
                else
                {
                    wrapper.SendOnly(message);
                }

                return new Result
                {
                    Success = true,
                    Output = acknowledgement,
                    Error = null,
                };
            }
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
        if (message.EndsWith('\r'))
            return message;

        return $"{message}\r";
    }
}