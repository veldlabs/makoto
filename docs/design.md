# Design

Makoto looks like FluentValidation at the call site, but the fluent code never runs. The generator reads
`Configure()` at compile time and emits straight-line code, so there is no reflection, no expression trees and no
rule objects to walk at runtime.

## Rules

A rule is a stateless class. It only takes data and evaluates it.

```csharp
[ValidationRule(Message = "'{PropertyName}' is not an email address.")]
public partial class IsEmailValidationRule : ValidationRule<string>
{
    public override bool Evaluate(string value) => EmailRegex.IsMatch(value);

    [GeneratedRegex("...")]
    private static partial Regex EmailRegex { get; }
}
```

The generator turns it into one extension method, named after the class without its `ValidationRule`/`Rule` suffix:

```csharp
public static class IsEmailValidationRuleExtensions
{
    private static readonly global::Demo.IsEmailValidationRule s_rule = new();

    [GeneratedRule(typeof(global::Demo.IsEmailValidationRule), "IsEmail", "'{PropertyName}' is not an email address.")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEmail(this string? value) => value is not null && s_rule.Evaluate(value);
}
```

The rule class is sealed (the generator adds `sealed` to partial rules, MKO002 otherwise). A sealed receiver held in a
`static readonly` is what lets the JIT drop the virtual call and inline `Evaluate`.

Null handling depends on the rule's value type:

| Rule | Receiver | Null |
| --- | --- | --- |
| `ValidationRule<string>` | `string?` | fails, `Evaluate` not called |
| `ValidationRule<string?>` | `string?` | passed to `Evaluate` |
| `ValidationRule<int>` | `int`, plus an `int?` overload | fails, `Evaluate` not called |
| `ValidationRule<T>` with `T : struct` | `T`, plus a `T?` overload | fails, `Evaluate` not called |
| `ValidationRule<T>` (open) | `T` | passed to `Evaluate` |

Arguments come from `ValidationRule<T, TArg>` and `ValidationRule<T, TArg1, TArg2>`, and keep the parameter names of
the `Evaluate` override: `x.MaxLength(10)`.

## Validators

```csharp
[Validator]
public sealed partial class SignupValidator : Validator<Signup>
{
    protected override void Configure()
    {
        Property(x => x.Email)
            .Require(x => x.IsEmail()).When(x => x is not null)
            .Require(x => x.Length < 200).WithMessage("Too long.").WithCode("TooLong");

        Property(x => x.Address).ValidateWith<AddressValidator>();

        Each(x => x.Tags).Require(x => x.NotEmpty());
    }
}
```

Semantics:

- `When`, `WithMessage` and `WithCode` apply to the `Require` right before them. Several `When`s are ANDed. Used before
  any `Require`, they are MKO018.
- `When(x => ...)` gets the value. `When((model, x) => ...)` and `Require((model, x) => ...)` also get the model.
- `StopOnFailure()` and `Named("...")` apply to the whole chain, wherever they appear.
- `Property` reads the selector once per chain. The reported name is the member path (`Address.City`), ignoring `!` and
  `?.`, unless `Named` overrides it.
- `Each` skips a null collection. Its failures are reported as `Tags[3]`, or `Items[3].Name` through `ValidateWith`.
- `ValidateWith<V>()` skips null values and calls `V`'s static entry points directly.
- Lambdas must be inline and must not capture locals, parameters of `Configure` or instance members (MKO013). Static
  members and constants of the validator are fine.
- The default code of a `Require` is the rule's code when its lambda is a single rule call on the parameter, otherwise
  `"Require"`. The same goes for the message, falling back to `'{PropertyName}' is not valid.`.
- `{PropertyName}` is replaced at compile time with the chain's name. For rule calls, `{argName}` is replaced with the
  argument's constant value, and left as is when the argument isn't constant.

The generator emits:

```csharp
sealed partial class SignupValidator
{
    private static readonly global::Makoto.ValidationFailure s_failure0 = new("Email", "IsEmail", "'Email' is not an email address.");
    private static readonly global::Makoto.ValidationFailure s_failure1 = new("Email", "TooLong", "Too long.");
    private static readonly global::Makoto.ValidationFailure s_failure2 = new("", "NotEmpty", "'Tags' must not be empty.");

    // Check first: valid input is the common case, and the collecting code is several times larger. Rules are pure,
    // so on invalid input the rules before the first failure simply run again.
    public override global::Makoto.ValidationResult Validate(global::Demo.Signup instance) => Check(instance) ? default : Collect(instance);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static global::Makoto.ValidationResult Collect(global::Demo.Signup instance)
    {
        var failures = default(global::Makoto.FailureCollector);
        ValidateInto(instance, ref failures);
        return failures.ToResult();
    }

    public override bool IsValid(global::Demo.Signup instance) => Check(instance);

    /// Short-circuits on the first failure.
    public static bool Check(global::Demo.Signup instance)
    {
        global::System.ArgumentNullException.ThrowIfNull(instance);

        var value0 = Select0(instance);
        if (When0(value0) && !Require0(value0)) return false;
        if (!Require1(value0)) return false;

        var value1 = Select1(instance);
        if (value1 is not null && !global::Demo.AddressValidator.Check(value1)) return false;

        // ...
        return true;
    }

    /// Collects every failure. Nested validators and Each add their path afterwards, so paths are
    /// only built on the failure path.
    public static void ValidateInto(global::Demo.Signup instance, ref global::Makoto.FailureCollector failures)
    {
        global::System.ArgumentNullException.ThrowIfNull(instance);

        var value0 = Select0(instance);
        if (When0(value0) && !Require0(value0)) failures.Add(s_failure0);
        if (!Require1(value0)) failures.Add(s_failure1);

        var value1 = Select1(instance);
        if (value1 is not null)
        {
            var start = failures.Count;
            global::Demo.AddressValidator.ValidateInto(value1, ref failures);
            failures.PrefixFrom(start, "Address");
        }

        var items = Select2(instance);
        if (items is not null)
        {
            var index = 0;
            foreach (var item in items)
            {
                var start = failures.Count;
                if (!Require2(item)) failures.Add(s_failure2);
                failures.PrefixFrom(start, "Tags", index);
                index++;
            }
        }
    }

    // User lambdas are copied verbatim into static methods. The file's usings are copied too, so they bind
    // exactly like the original. The JIT inlines these, and the call to the rule is a direct call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? Select0(global::Demo.Signup x) => x.Email;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Require0(string? x) => x.IsEmail();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool When0(string? x) => x is not null;
}
```

`ArgumentNullException.ThrowIfNull` is only emitted for reference-type models. `Each` should use a `for` over a span
for arrays and `List<T>`, and `foreach` for everything else.

### Why rule calls resolve by name

The generator runs before its own output exists, so for a rule in the same project `x.IsEmail()` is an unresolved call
in the semantic model. Nothing needs it to resolve to emit the lambda, because the lambda is copied as text. It only
matters for the default code and message:

1. If the call binds, it is a rule from a referenced assembly. Read `[GeneratedRule]` from the method.
2. Otherwise look the method name up in the same-project rule table from `RuleDiscovery`, preferring a rule whose
   namespace is in scope. No match means the generic defaults apply.

The type of a chain must come from the `Property`/`Each` call, not from `Require`, whose lambda may contain that
unresolved call.

### Dependency injection

When the compilation references `Microsoft.Extensions.DependencyInjection.Abstractions`, the generator emits one
`AddMakotoValidators(this IServiceCollection)` per assembly, in the namespace `Makoto`, with a `TryAddSingleton` per
validator. Validators are stateless, and no reflection or assembly scanning is involved. The class is internal so
assemblies don't clash.

## Non-goals for now

- Async rules. Rules take data and evaluate it, and anything that needs I/O doesn't belong here.
- Message localization. Messages are constants baked into the generated code.
- Attempted values on failures. Keeping them would box and rule out sharing one failure instance per rule.
