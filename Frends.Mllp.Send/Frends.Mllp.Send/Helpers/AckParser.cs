using System;
using Frends.Mllp.Send.Definitions;
using NHapi.Base.Model;
using NHapi.Base.Parser;
using NHapi.Base.Util;

namespace Frends.Mllp.Send.Helpers;

internal static class AckParser
{
    internal static AckParseResult Parse(string ackMessage)
    {
        if (string.IsNullOrWhiteSpace(ackMessage))
            return new AckParseResult(AckResultType.Invalid, null, "ACK message was empty.");

        try
        {
            var parser = new PipeParser();
            var parsed = parser.Parse(ackMessage);

            if (parsed is not IMessage message)
                return new AckParseResult(AckResultType.Invalid, null, "ACK could not be parsed as an HL7 message.");

            var terser = new Terser(message);
            var ackCode = terser.Get("/MSA-1");
            var errorDescription = terser.Get("/MSA-3");

            var resultType = ClassifyAckCode(ackCode);

            return new AckParseResult(resultType, ackCode, errorDescription);
        }
        catch (Exception ex)
        {
            return new AckParseResult(AckResultType.Invalid, null, $"Failed to parse ACK: {ex.Message}");
        }
    }

    internal static bool IsAcceptable(AckResultType resultType, AcceptableAckCodes acceptableAckCodes)
    {
        return acceptableAckCodes switch
        {
            AcceptableAckCodes.All => resultType is AckResultType.Accept or AckResultType.Error or AckResultType.Reject,
            AcceptableAckCodes.Success => resultType is AckResultType.Accept,
            AcceptableAckCodes.Error => resultType is AckResultType.Accept or AckResultType.Error,
            AcceptableAckCodes.Reject => resultType is AckResultType.Accept or AckResultType.Reject,
            _ => false,
        };
    }

    private static AckResultType ClassifyAckCode(string ackCode)
    {
        if (string.IsNullOrWhiteSpace(ackCode))
            return AckResultType.Invalid;

        return ackCode.ToUpperInvariant() switch
        {
            "AA" or "CA" => AckResultType.Accept,
            "AE" or "CE" => AckResultType.Error,
            "AR" or "CR" => AckResultType.Reject,
            _ => AckResultType.Invalid,
        };
    }
}