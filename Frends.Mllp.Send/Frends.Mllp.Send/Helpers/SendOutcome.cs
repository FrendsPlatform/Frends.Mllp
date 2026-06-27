using Frends.Mllp.Send.Definitions;

namespace Frends.Mllp.Send.Helpers
{
    internal sealed class SendOutcome
    {
        internal SendOutcome(string acknowledgement, AckResultType ackResultType, string ackCode, string ackErrorDescription)
        {
            Acknowledgement = acknowledgement;
            AckResultType = ackResultType;
            AckCode = ackCode;
            AckErrorDescription = ackErrorDescription;
        }

        internal string Acknowledgement { get; }

        internal AckResultType AckResultType { get; }

        internal string AckCode { get; }

        internal string AckErrorDescription { get; }
    }
}
