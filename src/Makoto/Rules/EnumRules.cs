namespace Makoto.Rules;

/// <summary>Passes when the value is a declared member of the enum. A combination of flags is not, unless it is declared. Null fails.</summary>
/// <typeparam name="T">The enum type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' is not a defined value.")]
public sealed class IsDefinedRule<T> : ValidationRule<T>
    where T : struct, Enum
{
    /// <inheritdoc />
    public override bool Evaluate(T value) => Enum.IsDefined(value);
}
