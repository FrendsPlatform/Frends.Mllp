using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Frends.Mllp.Send.Helpers
{
    internal sealed class ValidIpAddressAttribute : ValidationAttribute
    {
        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var str = value?.ToString();
            if (string.IsNullOrWhiteSpace(str))
                return ValidationResult.Success;

            return IPAddress.TryParse(str, out _)
                ? ValidationResult.Success
                : new ValidationResult("Invalid ListenAddress. Provide a valid IP address or leave the field empty.");
        }
    }
}
