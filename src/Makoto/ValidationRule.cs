namespace Makoto;

/// <summary>
/// A stateless check over a single value. Mark derived types with <see cref="ValidationRuleAttribute"/>
/// and the generator emits a direct extension method for them.
/// </summary>
/// <typeparam name="T">The value type. Use a nullable annotation to receive nulls in <see cref="Evaluate"/>.</typeparam>
public abstract class ValidationRule<T>
{
    /// <summary>Returns true when <paramref name="value"/> passes.</summary>
    public abstract bool Evaluate(T value);
}

/// <summary>A <see cref="ValidationRule{T}"/> that takes one argument, e.g. <c>x.MaxLength(10)</c>.</summary>
public abstract class ValidationRule<T, TArg>
{
    /// <summary>Returns true when <paramref name="value"/> passes.</summary>
    public abstract bool Evaluate(T value, TArg arg);
}

/// <summary>A <see cref="ValidationRule{T}"/> that takes two arguments, e.g. <c>x.Between(1, 5)</c>.</summary>
public abstract class ValidationRule<T, TArg1, TArg2>
{
    /// <summary>Returns true when <paramref name="value"/> passes.</summary>
    public abstract bool Evaluate(T value, TArg1 arg1, TArg2 arg2);
}
