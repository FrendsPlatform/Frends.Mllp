using System;
using System.IO;
using System.Text;

namespace Frends.Mllp.Send.Helpers;

/// <summary>
/// Handles logging of MLLP message send events.
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

        var path = string.IsNullOrWhiteSpace(logFilePath)
            ? Path.Combine(Path.GetTempPath(), $"frends-mllp-send{DateTime.UtcNow:yyyyMMdd-HHmmss}.log")
            : logFilePath;

        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            writer = new StreamWriter(path, append: true, Encoding.UTF8) { AutoFlush = true };

            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine($"MLLP Message Send Log - Session started at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            writer.WriteLine("=".PadRight(80, '='));
            writer.WriteLine();
        }
        catch (Exception ex)
        {
            this.enabled = false;
            System.Diagnostics.Trace.TraceWarning($"[MessageLogger] Failed to initialize log file '{path}': {ex.Message}. Logging disabled.");
        }
    }

    /// <summary>
    /// Disposes the logger and releases file resources. Should be called when the session ends.
    /// </summary>
    public void Dispose()
    {
        writer?.Dispose();
    }

    internal void LogMessageSent(string message, string host, int port)
    {
        var messageType = ExtractMessageType(message);
        Log(
            "MESSAGE SENT",
            sessionId: null,
            ("Host", $"{host}:{port}"),
            ("Size", $"{message?.Length ?? 0} characters"),
            ("Message Type", messageType),
            ("Content Preview", logContent ? GetPreview(message) : null));
    }

    internal void LogMessageSuccess(string message, string host, int port, bool ackReceived, string ackPreview)
    {
        Log(
            "SUCCESS",
            sessionId: null,
            ("Host", $"{host}:{port}"),
            ("ACK Received", ackReceived ? "Yes" : "No (ACK not expected)"),
            ("ACK Preview", logContent && ackReceived ? ackPreview : null));
    }

    internal void LogMessageFailure(string message, string host, int port, Exception ex)
    {
        Log(
            "FAILURE",
            sessionId: null,
            ("Host", $"{host}:{port}"),
            ("Error Type", ex?.GetType().Name),
            ("Error Message", ex?.Message),
            ("Message", logContent ? GetPreview(message) : null),
            ("Stack Trace", ex?.StackTrace));
    }

    internal void LogAcknowledgementFailure(string host, int port, Exception ex) =>
        Log(
            "ACK RECEIVE FAILURE",
            sessionId: null,
            ("Host", $"{host}:{port}"),
            ("Error", ex?.Message ?? "Connection lost while waiting for ACK"));

    internal void LogConnectionDropped(string host, int port, string reason) =>
        Log(
            "CONNECTION DROPPED",
            sessionId: null,
            ("Host", $"{host}:{port}"),
            ("Reason", reason));

    internal void LogMessageRejected(string host, int port, int messageSize, int limit) =>
    Log(
        "MESSAGE REJECTED",
        sessionId: null,
        ("Host", $"{host}:{port}"),
        ("Reason", "Message size exceeds limit"),
        ("Size", $"{messageSize} bytes"),
        ("Limit", $"{limit} bytes"));

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
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning(
                    $"[MessageLogger] Failed to write log entry: {ex.Message}");
            }
        }
    }
}