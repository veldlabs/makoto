using Makoto.Generators.Model;

namespace Makoto.Generators.Validators;

/// <summary>How a value is tested for null.</summary>
internal enum ValueKind
{
    /// <summary>A reference type: <c>is not null</c>.</summary>
    Reference,

    /// <summary>A non-nullable value type: never null.</summary>
    Value,

    /// <summary>A <c>Nullable&lt;T&gt;</c>: <c>is not null</c>, and unwrapped before it reaches a validator.</summary>
    NullableValue,
}

/// <summary>How an <c>Each</c> chain walks its collection.</summary>
internal enum CollectionKind
{
    /// <summary>Not an <c>Each</c> chain.</summary>
    None,
    Array,
    List,

    /// <summary>An <c>IList&lt;T&gt;</c> or <c>IReadOnlyList&lt;T&gt;</c>: indexed, so no enumerator is boxed.</summary>
    Indexable,

    /// <summary>An <c>ImmutableArray&lt;T&gt;</c>, whose default value throws from foreach and counts as empty.</summary>
    ImmutableArray,
    Enumerable,
}

/// <summary>A lambda copied as text into the private static method <see cref="Name"/>.</summary>
internal sealed record Lambda(string Name, EquatableArray<string> Parameters, string Body, bool IsBlock);

internal abstract record ChainStep;

/// <summary>
/// A <c>Require</c> and its modifiers. Code and Message are only what the chain states until the resolver fills
/// them in and clears Rule.
/// </summary>
internal sealed record RequireStep(
    int Index,
    Lambda Predicate,
    EquatableArray<Lambda> Conditions,
    string? Code,
    string? Message,
    RuleCall? Rule) : ChainStep;

internal sealed record ValidateWithStep(string Validator) : ChainStep;

/// <summary>A lambda body of the form <c>param.Rule(args)</c>. Constant is null when the argument isn't one.</summary>
internal sealed record RuleCallArgument(string? Name, string? Constant);

/// <summary>
/// A call that may be a rule. Bound calls come from a referenced assembly and carry what [GeneratedRule] says, if
/// anything. Unbound ones are matched against the same-project rules by the resolver.
/// </summary>
internal sealed record RuleCall(
    string Name,
    bool IsBound,
    string? Code,
    string? Message,
    EquatableArray<string> Parameters,
    EquatableArray<RuleCallArgument> Arguments);

internal sealed record RuleChain(
    string Name,
    string ValueType,
    ValueKind ValueKind,
    Lambda Selector,
    string SelectedType,
    CollectionKind Collection,
    ValueKind CollectionValueKind,
    bool StopOnFailure,
    EquatableArray<ChainStep> Steps);

/// <summary>One namespace level of the source file and the directives inside it. Name is null for the file itself.</summary>
internal sealed record NamespaceScope(string? Name, EquatableArray<string> Directives);

internal sealed record ValidatorModel(
    string ClassName,
    string QualifiedName,
    string ModelType,
    bool ModelIsReference,
    EquatableArray<NamespaceScope> Scopes,
    EquatableArray<string> ImportedNamespaces,
    EquatableArray<RuleChain> Chains);

/// <summary>The result of inspecting one [Validator] class. Model is null when it can't be emitted.</summary>
internal sealed record ValidatorResult(ValidatorModel? Model, EquatableArray<DiagnosticInfo> Diagnostics)
{
    public static readonly ValidatorResult Unusable = new(null, EquatableArray<DiagnosticInfo>.Empty);
}

internal sealed record Registration(string ModelType, string Validator);

/// <summary>What AddMakotoValidators registers. Empty for an assembly whose validators all have errors.</summary>
internal sealed record RegistrationSet(bool HasValidators, EquatableArray<Registration> Registrations);
