using System;
using Frends.Mllp.Send.Definitions;

namespace Frends.Mllp.Send.Helpers;

internal sealed class AckRejectedException : Exception
{
    internal AckRejectedException(string message, AckResultType ackResultType, string ackCode, string ackErrorDescription)
        : base(message)
    {
        AckResultType = ackResultType;
        AckCode = ackCode;
        AckErrorDescription = ackErrorDescription;
    }

    internal AckResultType AckResultType { get; }

    internal string AckCode { get; }

    internal string AckErrorDescription { get; }
}