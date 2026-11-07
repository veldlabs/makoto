using System.Text;

namespace Makoto.Rules;

/// <summary>
/// Passes when the string has a single <c>@</c> with something on both sides. Deliberately loose, like
/// FluentValidation's default: the only real check of an address is sending mail to it.
/// </summary>
[ValidationRule(Message = "'{PropertyName}' is not a valid email address.")]
public sealed class IsEmailRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && at == value.LastIndexOf('@');
    }
}

/// <summary>Passes when the string is an absolute http or https URL.</summary>
[ValidationRule(Message = "'{PropertyName}' is not a valid URL.")]
public sealed class IsUrlRule : ValidationRule<string>
{
    // The Uri is the one allocation in the built-in rules. The prefix check keeps it off the failure path, and rejects
    // the leading white space that Uri.TryCreate would trim.
    /// <inheritdoc />
    public override bool Evaluate(string value) =>
        (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        && Uri.TryCreate(value, UriKind.Absolute, out _);
}

/// <summary>Passes when the string parses as a <see cref="Guid"/>, in any format <see cref="Guid.TryParse(string, out Guid)"/> takes.</summary>
[ValidationRule(Message = "'{PropertyName}' is not a valid GUID.")]
public sealed class IsGuidRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value) => Guid.TryParse(value, out _);
}

/// <summary>Passes when the string is one or more ASCII digits. An empty string fails.</summary>
[ValidationRule(Message = "'{PropertyName}' must only contain digits.")]
public sealed class IsDigitsRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value) => value.Length != 0 && !value.AsSpan().ContainsAnyExceptInRange('0', '9');
}

/// <summary>Passes when every character is ASCII. An empty string passes: combine with <c>NotEmpty</c> to exclude it.</summary>
[ValidationRule(Message = "'{PropertyName}' must only contain ASCII characters.")]
public sealed class IsAsciiRule : ValidationRule<string>
{
    /// <inheritdoc />
    public override bool Evaluate(string value) => Ascii.IsValid(value);
}
