# Makoto

Validation for .NET with a FluentValidation-style API and no runtime machinery. A source generator reads your rules at
compile time and emits straight-line code: no reflection, no expression trees and no rule objects to walk. The
generated code allocates nothing when validation passes. Rules are the exception only if their own code allocates:
`IsUrl` builds a `Uri`, and a `Regex` under contention creates a new runner.

## Rules

A rule is a stateless class that takes data and evaluates it. The generator turns it into an extension method named
after the class, so `IsEmailValidationRule` becomes `IsEmail()`.

```csharp
[ValidationRule(Message = "'{PropertyName}' is not an email address.")]
public partial class IsEmailValidationRule : ValidationRule<string>
{
    public override bool Evaluate(string value) => EmailRegex.IsMatch(value);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex { get; }
}
```

Rules can take arguments through `ValidationRule<T, TArg>` and `ValidationRule<T, TArg1, TArg2>`, and they work on
their own too: `"a@b.co".IsEmail()`. Common rules ship in `Makoto.Rules`.

## Validators

```csharp
[Validator]
public sealed partial class SignupValidator : Validator<Signup>
{
    protected override void Configure()
    {
        Property(x => x.Email)
            .Require(x => x.IsEmail()).When(x => x is not null);

        Property(x => x.Password)
            .Require(x => x.MinLength(12))
            .Require((model, x) => x != model.Email).WithMessage("Don't use your email as a password.");

        Property(x => x.Address).ValidateWith<AddressValidator>();

        Each(x => x.Tags).Require(x => x.NotEmpty());
    }
}
```

`Configure` is never called. The generator emits `Validate`, `IsValid` and the static entry points `Check` and
`ValidateInto` into the partial class, which you use like any other validator:

```csharp
IValidator<Signup> validator = new SignupValidator();

ValidationResult result = validator.Validate(signup);
foreach (var failure in result.Failures)
{
    Console.WriteLine($"{failure.PropertyName}: {failure.Message}");
}

bool ok = SignupValidator.Check(signup); // no dispatch, stops at the first failure
```

## Dependency injection

Projects that reference `Microsoft.Extensions.DependencyInjection.Abstractions` get a generated registration for every
validator in the assembly, with no assembly scanning:

```csharp
services.AddMakotoValidators();
```

## Native AOT

Makoto is AOT and trim compatible. All the dynamic work happens at compile time, so published apps contain only plain
static code, including the generated DI registration.

## Performance

The same validator in both libraries: five properties, a nested validator and a three-item collection. Measured with
[BenchmarkDotNet](benchmarks/Makoto.Benchmarks/Program.cs) against FluentValidation 12.1.1, on .NET 10 and a Ryzen 9 7950X.

| | Mean | Allocated |
| --- | ---: | ---: |
| FluentValidation, valid | 686 ns | 2,240 B |
| Makoto `Validate`, valid | 6.1 ns | 0 B |
| Makoto through `IValidator<T>`, valid | 6.3 ns | 0 B |
| Makoto static `Check`, valid | 5.4 ns | 0 B |
| FluentValidation, invalid | 3,867 ns | 12,328 B |
| Makoto `Validate`, invalid | 73 ns | 400 B |

FluentValidation does more per failure: it records the attempted value and formats messages at runtime. Makoto's
messages are compile-time constants.

`Validate` runs `Check` first, because valid input is the common case. On invalid input, the rules before the first
failure therefore run twice, as do property getters. Keep rules and getters free of side effects.

At tier 1 the JIT inlines the whole chain into the caller, from the validator through the extension method to the
rule's `Evaluate`, so there is no virtual call left in Makoto's own code.

See [docs/design.md](docs/design.md) for the generated code and exact semantics.
