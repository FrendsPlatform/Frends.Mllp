using Frends.Mllp.Send.Definitions;

namespace Frends.Mllp.Send.Helpers
{
    internal sealed class AckParseResult
    {
        internal AckParseResult(AckResultType resultType, string ackCode, string errorDescription)
        {
            ResultType = resultType;
            AckCode = ackCode;
            ErrorDescription = errorDescription;
        }

        internal AckResultType ResultType { get; }

        internal string AckCode { get; }

        internal string ErrorDescription { get; }
    }
}
