using System.ComponentModel.DataAnnotations;

namespace SRC.Validation;

/// <summary>Accepts only dates strictly after today (UTC). Null passes; pair with [Required] when needed.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class FutureDateAttribute : ValidationAttribute
{
    public FutureDateAttribute() : base("{0} must be a date in the future.") { }

    public override bool IsValid(object? value) => value switch
    {
        null => true,
        DateOnly d => d > DateOnly.FromDateTime(DateTime.UtcNow),
        DateTime dt => dt > DateTime.UtcNow,
        _ => false
    };
}
