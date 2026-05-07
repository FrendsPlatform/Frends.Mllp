using Frends.Mllp.Receive.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class OptionsTests
{
    [Test]
    public void ShouldUseDefaultMllpFramingBytes()
    {
        var options = new Options();

        Assert.That(options.StartBlockByte, Is.EqualTo(11));
        Assert.That(options.EndBlockByte, Is.EqualTo(28));
        Assert.That(options.CarriageReturnByte, Is.EqualTo(13));
    }
}
