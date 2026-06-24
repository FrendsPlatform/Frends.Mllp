using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.Caching;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Frends.Mllp.Send.Definitions;
using Frends.Mllp.Send.Helpers;
using NHapi.Base.Parser;

namespace Frends.Mllp.Send;

/// <summary>
/// Task Class for Mllp operations.
/// </summary>
public static class Mllp
{
    private static readonly ObjectCache ConnectionCache = MemoryCache.Default;
    private static readonly object CacheLock = new();

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
        MessageLogger logger = null;
        X509Certificate2 clientCert = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger = new MessageLogger(options.EnableLogging, options.LogFilePath, options.LogMessageContent);

            var parser = options.ValidateWithNhapi ? new PipeParser() : null;
            var message = PrepareMessage(input.Hl7Message, parser);

            var messageBytes = GetEncoding(connection).GetByteCount(message);
            if (options.MaxMessageSize > 0 && messageBytes > options.MaxMessageSize)
            {
                logger.LogMessageRejected(connection.Host, connection.Port, messageBytes, options.MaxMessageSize);
                throw new ArgumentException(
                    $"Message size ({messageBytes} bytes) exceeds the configured limit ({options.MaxMessageSize} bytes).");
            }

            if (connection.TlsMode == TlsMode.Mtls)
            {
                if (string.IsNullOrEmpty(connection.ClientCertPath))
                    throw new ArgumentException("mTLS is enabled but client certificate path is missing.");

                clientCert = new X509Certificate2(connection.ClientCertPath, connection.ClientCertPassword);
            }

            var receiveTimeoutMs = (int)TimeSpan.FromSeconds(connection.ReadTimeoutSeconds).TotalMilliseconds;
            var outcome = SendWithRetry(connection, options, clientCert, message, logger, receiveTimeoutMs, cancellationToken);

            return new Result
            {
                Success = true,
                Output = outcome.Acknowledgement,
                AckResultType = outcome.AckResultType,
                AckCodeValue = outcome.AckCode,
                AckErrorDescription = outcome.AckErrorDescription,
                Error = null,
            };
        }
        catch (AckRejectedException ex)
        {
            logger?.LogMessageFailure(input?.Hl7Message, connection?.Host, connection?.Port ?? 0, ex);
            var result = ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure);
            result.AckResultType = ex.AckResultType;
            result.AckCodeValue = ex.AckCode;
            result.AckErrorDescription = ex.AckErrorDescription;
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogMessageFailure(input?.Hl7Message, connection?.Host, connection?.Port ?? 0, ex);
            return ErrorHandler.Handle(ex, options.ThrowErrorOnFailure, options.ErrorMessageOnFailure);
        }
        finally
        {
            logger?.Dispose();
            clientCert?.Dispose();
        }
    }

    private static SendOutcome SendWithRetry(
    Connection connection,
    Options options,
    X509Certificate2 clientCert,
    string message,
    MessageLogger logger,
    int receiveTimeoutMs,
    CancellationToken cancellationToken)
    {
        var totalAttempts = options.RetryCount + 1;
        Exception lastException = null;

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            try
            {
                if (options.KeepConnectionAlive)
                {
                    return SendWithKeepAlive(connection, options, clientCert, message, logger, receiveTimeoutMs, cancellationToken);
                }

                using var wrapper = CreateWrapper(connection, clientCert);
                return SendWithWrapper(wrapper, message, connection, options, logger, receiveTimeoutMs);
            }
            catch (AckRejectedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;

                if (attempt >= totalAttempts)
                    break;

                logger.LogRetryAttempt(connection.Host, connection.Port, attempt, totalAttempts, ex.Message);

                cancellationToken.ThrowIfCancellationRequested();
                if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(options.RetryIntervalSeconds)))
                    cancellationToken.ThrowIfCancellationRequested();
            }
        }

        throw lastException;
    }

    private static SendOutcome SendWithWrapper(
    MtlsMllpWrapper wrapper,
    string message,
    Connection connection,
    Options options,
    MessageLogger logger,
    int receiveTimeoutMs)
    {
        logger.LogMessageSent(message, connection.Host, connection.Port);

        if (!options.ExpectAcknowledgement)
        {
            wrapper.SendOnly(
                message,
                options.StartBlockByte,
                options.EndBlockByte,
                options.CarriageReturnByte);

            logger.LogMessageSuccess(message, connection.Host, connection.Port, ackReceived: false, ackPreview: null);
            return new SendOutcome(string.Empty, AckResultType.NotApplicable, null, null);
        }

        try
        {
            var ack = wrapper.Send(
                message,
                receiveTimeoutMs,
                options.StartBlockByte,
                options.EndBlockByte,
                options.CarriageReturnByte);

            var parsed = AckParser.Parse(ack);
            var accepted = AckParser.IsAcceptable(parsed.ResultType, options.AcceptableAckCodes);

            logger.LogMessageSuccess(message, connection.Host, connection.Port, ackReceived: true, ackPreview: ack);

            if (!accepted)
            {
                throw new AckRejectedException(
                    $"ACK was classified as {parsed.ResultType} (code: {parsed.AckCode}). {parsed.ErrorDescription}",
                    parsed.ResultType,
                    parsed.AckCode,
                    parsed.ErrorDescription);
            }

            return new SendOutcome(ack, parsed.ResultType, parsed.AckCode, parsed.ErrorDescription);
        }
        catch (Exception ex) when (IsAckException(ex))
        {
            logger.LogAcknowledgementFailure(connection.Host, connection.Port, ex);
            throw;
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

    private static bool IsAckException(Exception ex) =>
     ex is TimeoutException or OperationCanceledException
    || (ex is IOException && ex.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase));

    private static CachedConnection GetOrCreateConnection(
    Connection connection,
    Options options,
    X509Certificate2 clientCert)
    {
        var cacheKey = GetConnectionCacheKey(connection);

        lock (CacheLock)
        {
            if (ConnectionCache.Get(cacheKey) is CachedConnection cached)
                return cached;

            var wrapper = CreateWrapper(connection, clientCert);

            var entry = new CachedConnection(wrapper);
            var policy = new CacheItemPolicy
            {
                SlidingExpiration = TimeSpan.FromMinutes(options.ConnectionCacheExpirationMinutes),
                RemovedCallback = args =>
                {
                    (args.CacheItem.Value as CachedConnection)?.Dispose();
                },
            }
        ;

            ConnectionCache.Add(cacheKey, entry, policy);
            return entry;
        }
    }

    private static MtlsMllpWrapper CreateWrapper(Connection connection, X509Certificate2 clientCert)
    {
        var connectTimeoutMs = (int)TimeSpan.FromSeconds(connection.ConnectTimeoutSeconds).TotalMilliseconds;
        var wrapper = new MtlsMllpWrapper(connection.Host, connection.Port, GetEncoding(connection), connectTimeoutMs);

        if (connection.TlsMode == TlsMode.Mtls)
        {
            wrapper.EnableMtls(
                clientCert,
                connection.Host,
                connection.IgnoreServerCertificateErrors,
                connection.ServerCertificateThumbprints ?? []);
        }

        return wrapper;
    }

    private static SendOutcome SendWithKeepAlive(
    Connection connection,
    Options options,
    X509Certificate2 clientCert,
    string message,
    MessageLogger logger,
    int receiveTimeoutMs,
    CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var cacheKey = GetConnectionCacheKey(connection);
            var invalidateCacheEntry = false;
            var cached = GetOrCreateConnection(connection, options, clientCert);
            cached.Lock.Wait(cancellationToken);
            try
            {
                return SendWithWrapper(cached.Wrapper, message, connection, options, logger, receiveTimeoutMs);
            }
            catch (AckRejectedException)
            {
                throw;
            }
            catch (Exception ex) when (attempt == 0 && ex is IOException or System.Net.Sockets.SocketException)
            {
                logger.LogConnectionDropped(connection.Host, connection.Port, ex.Message);
                invalidateCacheEntry = true;
            }
            catch
            {
                invalidateCacheEntry = true;
                throw;
            }
            finally
            {
                cached.Lock.Release();
                if (invalidateCacheEntry)
                    ConnectionCache.Remove(cacheKey);
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    private static string GetConnectionCacheKey(Connection connection) =>
        $"mllp:{connection.Host}:{connection.Port}:{connection.TlsMode}:{connection.ClientCertPath}:{connection.Encoding}:{connection.EncodingInString ?? string.Empty}";
}
