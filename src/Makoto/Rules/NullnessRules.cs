namespace Makoto.Rules;

/// <summary>Passes when the value is not null. Binds to <c>T = int?</c> on nullable value types, so those work too.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must not be null.")]
public sealed class NotNullRule<T> : ValidationRule<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T value) => value is not null;
}
