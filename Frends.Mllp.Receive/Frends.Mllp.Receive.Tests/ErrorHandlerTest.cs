using System;
using System.Threading;
using System.Threading.Tasks;
using Frends.Mllp.Receive.Definitions;
using NUnit.Framework;

namespace Frends.Mllp.Receive.Tests;

[TestFixture]
public class ErrorHandlerTest
{
    private const string CustomErrorMessage = "CustomErrorMessage";

    [Test]
    public void Should_Throw_Error_When_ThrowErrorOnFailure_Is_True()
    {
        var ex = Assert.ThrowsAsync<Exception>(() =>
           Mllp.Receive(DefaultInput(), DefaultConnection(), DefaultOptions(), CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
    }

    [Test]
    public async Task Should_Return_Failed_Result_When_ThrowErrorOnFailure_Is_False()
    {
        var options = DefaultOptions();
        options.ThrowErrorOnFailure = false;
        var result = await Mllp.Receive(DefaultInput(), DefaultConnection(), options, CancellationToken.None);
        Assert.That(result.Success, Is.False);
    }

    [Test]
    public void Should_Append_Warnings_To_Message_When_Throwing()
    {
        var inner = new InvalidOperationException("boom");

        var ex = Assert.Throws<Exception>(() =>
            Frends.Mllp.Receive.Helpers.ErrorHandler.Handle(inner, true, string.Empty, ["w1", "w2"]));

        Assert.That(ex.Message, Is.EqualTo("boom Warnings: w1 | w2"));
        Assert.That(ex.InnerException, Is.SameAs(inner));
    }

    [Test]
    public void Should_Keep_Message_Unchanged_When_Throwing_Without_Warnings()
    {
        var ex = Assert.Throws<Exception>(() =>
            Frends.Mllp.Receive.Helpers.ErrorHandler.Handle(new InvalidOperationException("boom"), true, string.Empty));

        Assert.That(ex.Message, Is.EqualTo("boom"));
    }

    [Test]
    public void Should_Return_Warnings_And_Original_Exception_When_Not_Throwing()
    {
        var inner = new InvalidOperationException("boom");

        var result = Frends.Mllp.Receive.Helpers.ErrorHandler.Handle(inner, false, string.Empty, ["w1"]);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Warnings, Is.EqualTo(new[] { "w1" }));
        Assert.That(result.Error.AdditionalInfo, Is.SameAs(inner));
    }

    [Test]
    public void Should_Use_Custom_ErrorMessageOnFailure()
    {
        var options = DefaultOptions();
        options.ErrorMessageOnFailure = CustomErrorMessage;
        var ex = Assert.ThrowsAsync<Exception>(() =>
            Mllp.Receive(DefaultInput(), DefaultConnection(), options, CancellationToken.None));
        Assert.That(ex, Is.Not.Null);
        Assert.That(ex.Message, Contains.Substring(CustomErrorMessage));
    }

    private static Input DefaultInput() => new()
    {
        Port = 0, // Invalid value to cause an exception
    };

    private static Connection DefaultConnection() => new();

    private static Options DefaultOptions() => new()
    {
        ThrowErrorOnFailure = true,
        ErrorMessageOnFailure = string.Empty,
    };
}
