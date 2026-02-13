using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Frends.Mllp.Send.Tests
{
    internal static class Helpers
    {
        internal static async Task<string> ReadMllpMessage(Stream stream, CancellationToken cancellationToken)
        {
            const byte startBlock = 0x0b;
            const byte endBlock = 0x1c;
            const byte carriageReturn = 0x0d;

            var builder = new StringBuilder();
            var buffer = new byte[1];
            var started = false;

            while (await stream.ReadAsync(buffer, 0, 1, cancellationToken) > 0)
            {
                var current = buffer[0];

                if (current == startBlock)
                {
                    started = true;
                    continue;
                }

                if (!started) continue;

                if (current == endBlock)
                {
                    var nextByte = new byte[1];
                    var read = await stream.ReadAsync(nextByte, 0, 1, cancellationToken);

                    if (read > 0 && nextByte[0] == carriageReturn)
                    {
                        return builder.ToString();
                    }

                    return builder.ToString();
                }

                builder.Append((char)current);
            }

            throw new Exception("Connection closed before MLLP message was fully received.");
        }

        internal static string BuildTestMessage() =>
            "MSH|^~\\&|SendingApp|SendingFac|ReceivingApp|ReceivingFac|20250101010101||ADT^A01|MSG00001|P|2.5.1\r" +
            "EVN|A01|20250101010101\r" +
            "PID|1||12345^^^Hospital^MR||Doe^John||19800101|M";

        internal static string BuildAck(string controlId) =>
            $"MSH|^~\\&|Listener|ListenerFacility|Sender|SenderFacility|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK^A01|ACK0001|P|2.5.1\r" +
            $"MSA|AA|{controlId}\r";

        internal static string ExtractControlId(string hl7Message)
        {
            var segments = hl7Message.Split('\r', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (!segment.StartsWith("MSH|", StringComparison.Ordinal))
                    continue;

                var fields = segment.Split('|');
                if (fields.Length > 9)
                    return fields[9];
            }

            return string.Empty;
        }
    }
}
