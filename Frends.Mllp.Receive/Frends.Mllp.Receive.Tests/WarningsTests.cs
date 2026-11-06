using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class WarningsTests
{
    [Test]
    public void Diagnostics_Should_Cap_Warnings_And_Add_Single_Suppression_Notice()
    {
        var diagnostics = new Frends.Mllp.Receive.Helpers.Diagnostics();
        for (var i = 0; i < 100; i++)
            diagnostics.Warn($"w{i}");

        var warnings = diagnostics.ToArray();

        Assert.That(warnings, Has.Length.EqualTo(21));
        Assert.That(warnings[20], Is.EqualTo("Further warnings were suppressed."));
    }

    [Test]
    public async Task Should_Return_Empty_Warnings_On_Clean_Run()
    {
        var result = await Mllp.Receive(DefaultInput(), DefaultConnection(), new Options(), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Warnings, Is.Empty);
    }

    [Test]
    public async Task Should_Report_Logging_Initialization_Failure_As_Warning_And_Still_Succeed()
    {
        // A file used as a directory makes the log file impossible to create.
        var blocker = Path.GetTempFileName();
        try
        {
            var options = new Options
            {
                EnableLogging = true,
                LogFilePath = Path.Combine(blocker, "mllp.log"),
            };

            var result = await Mllp.Receive(DefaultInput(), DefaultConnection(), options, CancellationToken.None);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Warnings, Has.Length.EqualTo(1));
            Assert.That(result.Warnings[0], Does.Contain("Message logging disabled"));
        }
        finally
        {
            File.Delete(blocker);
        }
    }

    private static Input DefaultInput() => new()
    {
        ListenAddress = IPAddress.Loopback.ToString(),
        Port = Helpers.GetAvailablePort(),
    };

    private static Connection DefaultConnection() => new()
    {
        ListenDurationSeconds = 1,
        BufferSize = 1024,
    };
}
