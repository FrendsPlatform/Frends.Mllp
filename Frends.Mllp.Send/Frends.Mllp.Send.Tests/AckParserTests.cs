using System;
using Frends.Mllp.Send.Definitions;
using Frends.Mllp.Send.Helpers;
using NUnit.Framework;

namespace Frends.Mllp.Send.Tests
{
    [TestFixture]
    public class AckParserTests
    {
        [TestCase("AA", AckResultType.Accept)]
        [TestCase("AE", AckResultType.Error)]
        [TestCase("AR", AckResultType.Reject)]
        [TestCase("XX", AckResultType.Invalid)]
        public void ShouldClassifyAckCode(string ackCode, AckResultType expected)
        {
            var ack = BuildAck(ackCode, "some detail");
            var result = AckParser.Parse(ack);

            Assert.That(result.ResultType, Is.EqualTo(expected));
            if (expected != AckResultType.Invalid)
                Assert.That(result.AckCode, Is.EqualTo(ackCode));
        }

        [Test]
        public void ShouldClassifyEmptyOrMalformedMessageAsInvalid()
        {
            Assert.That(AckParser.Parse(string.Empty).ResultType, Is.EqualTo(AckResultType.Invalid));
            Assert.That(AckParser.Parse("garbage|||not hl7").ResultType, Is.EqualTo(AckResultType.Invalid));
        }

        [TestCase(AckResultType.Accept, AcceptableAckCodes.Success, true)]
        [TestCase(AckResultType.Error, AcceptableAckCodes.Success, false)]
        [TestCase(AckResultType.Error, AcceptableAckCodes.Error, true)]
        [TestCase(AckResultType.Reject, AcceptableAckCodes.Error, false)]
        [TestCase(AckResultType.Reject, AcceptableAckCodes.All, true)]
        public void ShouldEvaluateAcceptability(AckResultType resultType, AcceptableAckCodes acceptableAckCodes, bool expected)
        {
            Assert.That(AckParser.IsAcceptable(resultType, acceptableAckCodes), Is.EqualTo(expected));
        }

        private static string BuildAck(string ackCode, string errorDescription = "") =>
            $"MSH|^~\\&|Listener|ListenerFacility|Sender|SenderFacility|{DateTime.UtcNow:yyyyMMddHHmmss}||ACK^A01|ACK0001|P|2.5.1\r" +
            $"MSA|{ackCode}|MSG00001|{errorDescription}\r";
    }
}
