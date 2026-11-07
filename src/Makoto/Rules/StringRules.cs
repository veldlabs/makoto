namespace Makoto.Rules;

/// <summary>Passes when the string has at least one character.</summary>
[ValidationRule(Message = "'{PropertyName}' must not be empty.")]
public sealed class NotEmptyRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value) => value.Length != 0;
}

/// <summary>Passes when the string has a character that is not white space. An empty string fails.</summary>
[ValidationRule(Message = "'{PropertyName}' must not be blank.")]
public sealed class NotBlankRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value) => !value.AsSpan().IsWhiteSpace();
}

/// <summary>Passes when the string has at least <c>min</c> UTF-16 code units.</summary>
[ValidationRule(Message = "'{PropertyName}' must be at least {min} characters long.")]
public sealed class MinLengthRule : ValidationRule<string, int>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, int min) => value.Length >= min;
}

/// <summary>Passes when the string has at most <c>max</c> UTF-16 code units.</summary>
[ValidationRule(Message = "'{PropertyName}' must be at most {max} characters long.")]
public sealed class MaxLengthRule : ValidationRule<string, int>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, int max) => value.Length <= max;
}

/// <summary>Passes when the string has between <c>min</c> and <c>max</c> UTF-16 code units, inclusive.</summary>
[ValidationRule(Message = "'{PropertyName}' must be between {min} and {max} characters long.")]
public sealed class LengthBetweenRule : ValidationRule<string, int, int>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, int min, int max) => value.Length >= min && value.Length <= max;
}

// StartsWithText and EndsWithText, not StartsWith and EndsWith: an instance method always beats an extension, so
// x.StartsWith("a") would silently call string.StartsWith, which is culture-sensitive and throws on null.

/// <summary>Passes when the string starts with <c>prefix</c>, compared ordinally.</summary>
[ValidationRule(Message = "'{PropertyName}' must start with '{prefix}'.")]
public sealed class StartsWithTextRule : ValidationRule<string, string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, string prefix) => value.AsSpan().StartsWith(prefix);
}

/// <summary>Passes when the string ends with <c>suffix</c>, compared ordinally.</summary>
[ValidationRule(Message = "'{PropertyName}' must end with '{suffix}'.")]
public sealed class EndsWithTextRule : ValidationRule<string, string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, string suffix) => value.AsSpan().EndsWith(suffix);
}

/// <summary>Passes when the string contains <c>text</c>, compared ordinally.</summary>
[ValidationRule(Message = "'{PropertyName}' must contain '{text}'.")]
public sealed class ContainsTextRule : ValidationRule<string, string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value, string text) => value.Contains(text);
}
