using System;
using Frends.Mllp.Receive.Definitions;

namespace Frends.Mllp.Receive.Helpers;

internal static class ErrorHandler
{
    internal static Result Handle(Exception exception, bool throwOnFailure, string errorMessageOnFailure, string[] warnings = null)
    {
        warnings ??= [];

        if (throwOnFailure)
        {
            var message = string.IsNullOrEmpty(errorMessageOnFailure) ? exception.Message : errorMessageOnFailure;

            // A thrown exception has no Warnings property, so the warnings go into the message to stay visible.
            if (warnings.Length > 0)
                message += $" Warnings: {string.Join(" | ", warnings)}";

            throw new Exception(message, exception);
        }

        var errorMessage = string.IsNullOrEmpty(errorMessageOnFailure)
            ? exception.Message
            : $"{errorMessageOnFailure}: {exception.Message}";

        var error = new Error
        {
            Message = errorMessage,
            AdditionalInfo = exception,
        };

        return new Result { Success = false, Warnings = warnings, Error = error };
    }
}
