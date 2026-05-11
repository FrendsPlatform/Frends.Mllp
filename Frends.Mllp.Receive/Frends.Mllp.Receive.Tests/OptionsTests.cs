using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class OptionsTests
{
    [Test]
    public async Task ShouldReceiveMessageWithCustomFramingBytes()
    {
        var port = Helpers.GetAvailablePort();
        var input = new Input
        {
            ListenAddress = IPAddress.Loopback.ToString(),
            Port = port,
        };
        var connection = new Connection
        {
            ListenDurationSeconds = 10,
            BufferSize = 1024,
        };
        var options = new Options
        {
            StartBlockByte = 1,
            EndBlockByte = 2,
            CarriageReturnByte = 3,
        };

        var sender = Task.Run(async () =>
        {
            await Task.Delay(100);
            await Helpers.SendMessageAsync(
                port,
                "MSH|^~\\&|HIS|RIH|EKG|EKG|198808181126|SECURITY|ADT^A01|MSG00001|P|2.5",
                startBlock: 1,
                endBlock: 2,
                carriageReturn: 3);
        });

        var result = await Mllp.Receive(
            input,
            connection,
            options,
            CancellationToken.None);
        await sender;

        Assert.That(result.Success, Is.True);
        Assert.That(result.Output, Has.Length.EqualTo(1));
        Assert.That(result.Output.First(), Does.Contain("MSH|^~\\&|HIS|RIH"));
    }
}
