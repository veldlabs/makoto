using System.Buffers;
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

/// <summary>
/// Passes when the string is an absolute http or https URL with no white space or control characters. Pair it with
/// <c>MaxLength</c> on untrusted input: parsing is linear, but a <see cref="Uri"/> holds several copies of the text.
/// </summary>
[ValidationRule(Message = "'{PropertyName}' is not a valid URL.")]
public sealed class IsUrlRule : ValidationRule<string>
{
    // Uri.TryCreate trims leading white space and percent-escapes any space, tab or newline inside the text, so a
    // value with a line break in it would pass as "valid". C0 controls and DEL are rejected for the same reason.
    private static readonly SearchValues<char> s_rejected = SearchValues.Create(
        "\u0000\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008\u0009\u000A\u000B\u000C\u000D\u000E\u000F" +
        "\u0010\u0011\u0012\u0013\u0014\u0015\u0016\u0017\u0018\u0019\u001A\u001B\u001C\u001D\u001E\u001F" +
        " \u007F");

    // The Uri is the one allocation in the built-in rules. The checks before it keep it off the failure path.
    /// <inheritdoc />
    public override bool Evaluate(string value) =>
        (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        && !value.AsSpan().ContainsAny(s_rejected)
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
