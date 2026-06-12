using System.ComponentModel.DataAnnotations;

namespace Frends.Mllp.Receive.Helpers
{
    internal sealed class RequiredIfAttribute : ValidationAttribute
    {
        private readonly string dependentProperty;
        private readonly object targetValue;

        public RequiredIfAttribute(string dependentProperty, object targetValue)
        {
            this.dependentProperty = dependentProperty;
            this.targetValue = targetValue;
        }

        protected override ValidationResult IsValid(object value, ValidationContext context)
        {
            var property = context.ObjectType.GetProperty(dependentProperty);
            var dependentValue = property?.GetValue(context.ObjectInstance);

            if (dependentValue?.Equals(targetValue) == true)
            {
                if (string.IsNullOrWhiteSpace(value?.ToString()))
                    return new ValidationResult(ErrorMessage ?? $"{context.DisplayName} is required.");
            }

            return ValidationResult.Success;
        }
    }
}
