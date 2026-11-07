namespace Makoto;

/// <summary>Generates an extension method for a <see cref="ValidationRule{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValidationRuleAttribute : Attribute
{
    /// <summary>The extension method name. Defaults to the class name without its <c>ValidationRule</c> or <c>Rule</c> suffix.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The default failure message. <c>{PropertyName}</c> and <c>{argName}</c> placeholders are resolved at
    /// compile time, the latter when the argument is a constant.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>The failure code. Defaults to the extension method name.</summary>
    public string? Code { get; set; }
}

/// <summary>Generates the validation code for a <see cref="Validator{T}"/>.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ValidatorAttribute : Attribute;

/// <summary>
/// Emitted by the generator on rule extension methods, so validators in other assemblies can find a rule's
/// defaults. Not meant to be applied by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class GeneratedRuleAttribute(Type rule, string code, string message) : Attribute
{
    /// <summary>The rule type this method calls.</summary>
    public Type Rule { get; } = rule;

    /// <summary>The default failure code.</summary>
    public string Code { get; } = code;

    /// <summary>The default failure message template.</summary>
    public string Message { get; } = message;
}
