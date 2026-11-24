using System.ComponentModel.DataAnnotations;

namespace server.Common.Api;

/// <summary>Like <see cref="StringLengthAttribute"/>, but ignores whitespace around the text.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public class TrimmedLengthAttribute(int minimum, int maximum)
    : ValidationAttribute($"The field {{0}} must be between {minimum} and {maximum} characters long.")
{
    // Null is left to [Required], as with the built-in attributes.
    public override bool IsValid(object? value) =>
        value is not string text || (text.Trim().Length >= minimum && text.Trim().Length <= maximum);
}
