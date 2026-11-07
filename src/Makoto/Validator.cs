namespace Makoto;

/// <summary>Validates instances of <typeparamref name="T"/>. Register and inject this, or mock it in tests.</summary>
public interface IValidator<in T>
{
    /// <summary>Runs every rule and collects all failures.</summary>
    ValidationResult Validate(T instance);

    /// <summary>Stops at the first failure. The generated code allocates nothing, though a rule's own code may.</summary>
    bool IsValid(T instance);
}

/// <summary>
/// Base class for validators. Mark derived types with <see cref="ValidatorAttribute"/> and declare the rules in
/// <see cref="Configure"/>. That method is only read by the generator and never runs.
/// </summary>
public abstract class Validator<T> : IValidator<T>
{
    /// <summary>Declares the rules. Read at compile time, never called.</summary>
    protected abstract void Configure();

    /// <inheritdoc />
    public abstract ValidationResult Validate(T instance);

    /// <inheritdoc />
    public abstract bool IsValid(T instance);

    /// <summary>Starts a rule chain for a property.</summary>
    protected PropertyRule<T, TProperty> Property<TProperty>(Func<T, TProperty> selector) => throw DesignTimeOnly();

    /// <summary>Starts a rule chain that applies to each element of a collection.</summary>
    protected PropertyRule<T, TElement> Each<TElement>(Func<T, IEnumerable<TElement>?> selector) => throw DesignTimeOnly();

    internal static NotSupportedException DesignTimeOnly() =>
        new("Validator rules are read at compile time and must not run. Is the validator marked [Validator] and partial?");
}

/// <summary>Design-time description of a rule chain. The generator reads these calls; none of them run.</summary>
public sealed class PropertyRule<T, TProperty>
{
    private PropertyRule()
    {
    }

    /// <summary>Adds a check on the value.</summary>
    public PropertyRule<T, TProperty> Require(Func<TProperty, bool> predicate) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Adds a check on the value that can also read the model.</summary>
    public PropertyRule<T, TProperty> Require(Func<T, TProperty, bool> predicate) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Only runs the preceding <c>Require</c> when the value matches.</summary>
    public PropertyRule<T, TProperty> When(Func<TProperty, bool> condition) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Only runs the preceding <c>Require</c> when the model and value match.</summary>
    public PropertyRule<T, TProperty> When(Func<T, TProperty, bool> condition) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Overrides the preceding <c>Require</c>'s message. Must be a constant.</summary>
    public PropertyRule<T, TProperty> WithMessage(string message) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Overrides the preceding <c>Require</c>'s code. Must be a constant.</summary>
    public PropertyRule<T, TProperty> WithCode(string code) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Overrides the reported property name for this chain. Must be a constant.</summary>
    public PropertyRule<T, TProperty> Named(string name) => throw Validator<T>.DesignTimeOnly();

    /// <summary>Skips the rest of this chain after its first failure.</summary>
    public PropertyRule<T, TProperty> StopOnFailure() => throw Validator<T>.DesignTimeOnly();

    /// <summary>Runs another generated validator on the value, skipping nulls.</summary>
    public PropertyRule<T, TProperty> ValidateWith<TValidator>() where TValidator : class => throw Validator<T>.DesignTimeOnly();
}
