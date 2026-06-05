using System;
using System.IO;
using System.Text;

namespace Frends.Mllp.Receive.Helpers;

/// <summary>
/// Handles logging of MLLP message processing events.
/// </summary>
internal sealed class MessageLogger : IDisposable
{
    private readonly bool enabled;
    private readonly bool logContent;
    private readonly StreamWriter writer;
    private readonly object lockObj = new();

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

            writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };

            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine($"MLLP Message Processing Log - Session started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
        }
        catch
        {
            enabled = false;
        }
    }

    /// <summary>
    /// Disposes the logger and releases file resources. Should be called when the session ends.
    /// </summary>
    public void Dispose()
    {
        writer?.Dispose();
    }

    internal void LogMessageReceived(string message, string sessionId)
    {
        var messageType = ExtractMessageType(message);
        Log(
            "MESSAGE RECEIVED",
            sessionId,
            ("Size", $"{message?.Length ?? 0} characters"),
            ("Message Type", messageType),
            ("Content Preview", logContent ? GetPreview(message) : null));
    }

    internal void LogMessageSuccess(string message, string sessionId, bool ackSent, string reason)
    {
        var ackInfo = ackSent ? "Yes" : $"No ({reason})";
        Log("SUCCESS", sessionId, ("ACK Sent", ackInfo));
    }

    internal void LogMessageFailure(string message, string sessionId, Exception ex)
    {
        Log(
            "FAILURE",
            sessionId,
            ("Error Type", ex?.GetType().Name),
            ("Error Message", ex?.Message),
            ("Message", logContent ? GetPreview(message) : null),
            ("Stack Trace", ex?.StackTrace));
    }

    internal void LogAcknowledgementFailure(string sessionId, Exception ex) =>
        Log("ACK SEND FAILURE", sessionId, ("Error", ex?.Message ?? "Client disconnected"));

    internal void LogConnectionRejected(string sessionId, string reason) =>
        Log("CONNECTION REJECTED", sessionId, ("Reason", reason));

    internal void LogMessageRejected(string sessionId, string reason, int messageSize) =>
        Log("MESSAGE REJECTED", sessionId, ("Reason", reason), ("Size", $"{messageSize} bytes"));

    internal void LogFramingError(string sessionId, string error) =>
        Log("FRAMING ERROR", sessionId, ("Error", error));

    internal void LogConnectionDropped(string sessionId, string reason) =>
        Log("CONNECTION DROPPED", sessionId, ("Reason", reason));

    private static string GetPreview(string message)
    {
        if (string.IsNullOrEmpty(message))
            return null;

        const int maxLength = 200;
        var preview = message.Length > maxLength
            ? string.Concat(message.AsSpan(0, maxLength), "...")
            : message;

        return preview.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string ExtractMessageType(string message)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("MSH"))
            return null;

        try
        {
            var segments = message.Split('|');
            return segments.Length > 8 ? segments[8] : null;
        }
        catch
        {
            return null;
        }
    }

    private void Log(string eventType, string sessionId, params (string Key, object Value)[] details)
    {
        if (!enabled)
            return;

        lock (lockObj)
        {
            try
            {
                writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {eventType}" +
                                 (sessionId != null ? $" | Session: {sessionId}" : string.Empty));

                foreach (var (key, value) in details)
                {
                    if (value != null)
                        writer.WriteLine($"  {key}: {value}");
                }

                writer.WriteLine();
            }
            catch
            {
                // Ignore
            }
        }
    }
}