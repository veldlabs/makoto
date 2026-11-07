namespace Makoto.Rules;

// On an int? receiver an open T binds to int?, which is not IComparable<int?>, so that rule drops out of overload
// resolution. Each rule therefore has a T? twin under the same name. The twin can't bind to an int receiver, since an
// extension's receiver takes no nullable conversion, so exactly one of the two applies. Null fails in both, which is
// what ValidationRule<int> does for int?. The null check is free for value types, where the JIT removes it.

/// <summary>Passes when the value is greater than <c>limit</c>. Null fails.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must be greater than {limit}.")]
public sealed class GreaterThanRule<T> : ValidationRule<T, T>
    where T : IComparable<T>?
{
    /// <inheritdoc />
    public override bool Evaluate(T value, T limit) => value is not null && value.CompareTo(limit) > 0;
}

/// <summary><see cref="GreaterThanRule{T}"/> for nullable value types. Null fails.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[ValidationRule(Name = "GreaterThan", Message = "'{PropertyName}' must be greater than {limit}.")]
public sealed class GreaterThanNullableRule<T> : ValidationRule<T?, T>
    where T : struct, IComparable<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T? value, T limit) => value.HasValue && value.GetValueOrDefault().CompareTo(limit) > 0;
}

/// <summary>Passes when the value is greater than or equal to <c>limit</c>. Null fails.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must be greater than or equal to {limit}.")]
public sealed class GreaterThanOrEqualRule<T> : ValidationRule<T, T>
    where T : IComparable<T>?
{
    /// <inheritdoc />
    public override bool Evaluate(T value, T limit) => value is not null && value.CompareTo(limit) >= 0;
}

/// <summary><see cref="GreaterThanOrEqualRule{T}"/> for nullable value types. Null fails.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[ValidationRule(Name = "GreaterThanOrEqual", Message = "'{PropertyName}' must be greater than or equal to {limit}.")]
public sealed class GreaterThanOrEqualNullableRule<T> : ValidationRule<T?, T>
    where T : struct, IComparable<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T? value, T limit) => value.HasValue && value.GetValueOrDefault().CompareTo(limit) >= 0;
}

/// <summary>Passes when the value is less than <c>limit</c>. Null fails.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must be less than {limit}.")]
public sealed class LessThanRule<T> : ValidationRule<T, T>
    where T : IComparable<T>?
{
    /// <inheritdoc />
    public override bool Evaluate(T value, T limit) => value is not null && value.CompareTo(limit) < 0;
}

/// <summary><see cref="LessThanRule{T}"/> for nullable value types. Null fails.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[ValidationRule(Name = "LessThan", Message = "'{PropertyName}' must be less than {limit}.")]
public sealed class LessThanNullableRule<T> : ValidationRule<T?, T>
    where T : struct, IComparable<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T? value, T limit) => value.HasValue && value.GetValueOrDefault().CompareTo(limit) < 0;
}

/// <summary>Passes when the value is less than or equal to <c>limit</c>. Null fails.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must be less than or equal to {limit}.")]
public sealed class LessThanOrEqualRule<T> : ValidationRule<T, T>
    where T : IComparable<T>?
{
    /// <inheritdoc />
    public override bool Evaluate(T value, T limit) => value is not null && value.CompareTo(limit) <= 0;
}

/// <summary><see cref="LessThanOrEqualRule{T}"/> for nullable value types. Null fails.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[ValidationRule(Name = "LessThanOrEqual", Message = "'{PropertyName}' must be less than or equal to {limit}.")]
public sealed class LessThanOrEqualNullableRule<T> : ValidationRule<T?, T>
    where T : struct, IComparable<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T? value, T limit) => value.HasValue && value.GetValueOrDefault().CompareTo(limit) <= 0;
}

/// <summary>Passes when the value is between <c>min</c> and <c>max</c>, inclusive. Null fails.</summary>
/// <typeparam name="T">The value type.</typeparam>
[ValidationRule(Message = "'{PropertyName}' must be between {min} and {max}.")]
public sealed class BetweenRule<T> : ValidationRule<T, T, T>
    where T : IComparable<T>?
{
    /// <inheritdoc />
    public override bool Evaluate(T value, T min, T max) =>
        value is not null && value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
}

/// <summary><see cref="BetweenRule{T}"/> for nullable value types. Null fails.</summary>
/// <typeparam name="T">The underlying value type.</typeparam>
[ValidationRule(Name = "Between", Message = "'{PropertyName}' must be between {min} and {max}.")]
public sealed class BetweenNullableRule<T> : ValidationRule<T?, T, T>
    where T : struct, IComparable<T>
{
    /// <inheritdoc />
    public override bool Evaluate(T? value, T min, T max) =>
        value.HasValue && value.GetValueOrDefault().CompareTo(min) >= 0 && value.GetValueOrDefault().CompareTo(max) <= 0;
}
