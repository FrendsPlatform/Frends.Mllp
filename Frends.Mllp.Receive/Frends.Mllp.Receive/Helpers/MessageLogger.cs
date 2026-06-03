using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Handles logging of MLLP message processing events.
/// </summary>
internal sealed class MessageLogger : IDisposable
{
    private readonly bool enabled;
    private readonly bool logContent;
    private readonly StreamWriter writer;
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public MessageLogger(bool enabled, string logFilePath, bool logContent)
    {
        this.enabled = enabled;
        this.logContent = logContent;

        if (!enabled)
            return;

        try
        {
            var path = string.IsNullOrWhiteSpace(logFilePath)
                ? Path.Combine(Path.GetTempPath(), $"frends-mllp-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log")
                : logFilePath;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            writer = new StreamWriter(path, append: true, Encoding.UTF8)
            {
                AutoFlush = true,
            };

            LogHeader();
        }
        catch (Exception ex)
        {
            enabled = false;
            Console.WriteLine($"Failed to initialize MLLP logger: {ex.Message}");
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    public void Dispose()
    {
        if (enabled && writer != null)
        {
            try
            {
                writer.Flush();
                writer.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        semaphore?.Dispose();
    }

    internal void LogMessageReceived(string message, string sessionId)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] MESSAGE RECEIVED");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Size: {message?.Length ?? 0} characters");

            if (logContent && !string.IsNullOrEmpty(message))
            {
                writer.WriteLine($"  Content Preview: {GetPreview(message)}");
            }

            var messageType = ExtractMessageType(message);
            if (!string.IsNullOrEmpty(messageType))
                writer.WriteLine($"  Message Type: {messageType}");

            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogMessageSuccess(string message, string sessionId, bool ackSent, string reason)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            var info = ackSent ? "ACK Sent: Yes" : "ACK Sent: No";
            if (!string.IsNullOrEmpty(reason))
                info += $" ({reason})";

            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] SUCCESS | Session: {sessionId} | {info}");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogMessageFailure(string message, string sessionId, Exception ex)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] FAILURE");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Error Type: {ex?.GetType().Name ?? "Unknown"}");
            writer.WriteLine($"  Error Message: {ex?.Message ?? "No details available"}");

            if (logContent && !string.IsNullOrEmpty(message))
            {
                writer.WriteLine($"  Message: {GetPreview(message)}");
            }

            if (ex?.StackTrace != null)
            {
                writer.WriteLine($"  Stack Trace:");
                writer.WriteLine($"    {ex.StackTrace.Replace("\n", "\n    ")}");
            }

            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogAcknowledgementFailure(string sessionId, Exception ex)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] ACK SEND FAILURE");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Error: {ex?.Message ?? "Client disconnected"}");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogConnectionRejected(string sessionId, string reason)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] CONNECTION REJECTED");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Reason: {reason}");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogMessageRejected(string sessionId, string reason, int messageSize)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] MESSAGE REJECTED");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Reason: {reason}");
            writer.WriteLine($"  Size: {messageSize} bytes");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogFramingError(string sessionId, string error)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] FRAMING ERROR");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Error: {error}");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogConnectionDropped(string sessionId, string reason)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] CONNECTION DROPPED");
            writer.WriteLine($"  Session ID: {sessionId}");
            writer.WriteLine($"  Reason: {reason}");
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    internal void LogSessionSummary(int totalMessages, int successCount, int failureCount)
    {
        if (!enabled)
            return;

        semaphore.Wait();
        try
        {
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine($"Session Summary - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            writer.WriteLine($"  Total Messages: {totalMessages}");
            writer.WriteLine($"  Successful: {successCount}");
            writer.WriteLine($"  Failed: {failureCount}");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
        }
        catch
        {
            // Silently ignore logging errors
        }
        finally
        {
            semaphore.Release();
        }
    }

    private void LogHeader()
    {
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine($"MLLP Message Processing Log - Session started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        writer.WriteLine("=".PadRight(80, '='));
        writer.WriteLine();
    }

    private static string GetPreview(string message)
    {
        if (string.IsNullOrEmpty(message))
            return "(empty)";

        const int maxLength = 200;
        var preview = message.Length > maxLength
            ? message.Substring(0, maxLength) + "..."
            : message;

        return preview.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string ExtractMessageType(string message)
    {
        if (string.IsNullOrEmpty(message))
            return null;

        try
        {
            if (message.StartsWith("MSH"))
            {
                var segments = message.Split('|');
                if (segments.Length > 8)
                {
                    return segments[8];
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }
}