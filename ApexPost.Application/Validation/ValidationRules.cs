using System.Text.RegularExpressions;

namespace ApexPost.Application.Validation;

/// <summary>Base class for the custom validation attributes. The validator finds these with reflection.</summary>
public abstract class ValidationRuleAttribute : Attribute
{
    public string? ErrorMessage { get; set; }

    public abstract bool IsValid(object? value);

    protected abstract string DefaultMessage(string memberName);

    public string MessageFor(string memberName) => ErrorMessage ?? DefaultMessage(memberName);
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class NotBlankAttribute : ValidationRuleAttribute
{
    public override bool IsValid(object? value) => value is string text && !string.IsNullOrWhiteSpace(text);

    protected override string DefaultMessage(string memberName) => $"{memberName} is required.";
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class TextLengthAttribute(int maxLength) : ValidationRuleAttribute
{
    public int MaxLength { get; } = maxLength;

    public override bool IsValid(object? value) => value is null || (value is string text && text.Length <= MaxLength);

    protected override string DefaultMessage(string memberName) =>
        $"{memberName} must be at most {MaxLength} characters.";
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class PositiveNumberAttribute : ValidationRuleAttribute
{
    public override bool IsValid(object? value) => value switch
    {
        int number => number > 0,
        long number => number > 0,
        decimal number => number > 0m,
        double number => number > 0d,
        _ => false
    };

    protected override string DefaultMessage(string memberName) => $"{memberName} must be greater than zero.";
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class PatternAttribute(string pattern) : ValidationRuleAttribute
{
    // The timeout stops a crafted input from keeping the CPU busy (ReDoS).
    private readonly Regex _regex = new(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));

    public override bool IsValid(object? value)
    {
        if (value is null)
            return true;

        if (value is not string text)
            return false;

        try
        {
            return _regex.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    protected override string DefaultMessage(string memberName) => $"{memberName} is not in a valid format.";
}
