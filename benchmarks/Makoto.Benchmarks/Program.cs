using Makoto;
using Makoto.Rules;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using FluentValidation;

BenchmarkSwitcher.FromAssembly(typeof(ValidationBenchmarks).Assembly).Run(args);

public sealed record Address(string City, string Zip);

public sealed record Signup(string? Email, string Name, int Age, Address? Address, List<string> Tags);

[Validator]
public sealed partial class MakotoAddressValidator : Validator<Address>
{
    protected override void Configure()
    {
        Property(x => x.City).Require(x => x.NotEmpty());
        Property(x => x.Zip).Require(x => x.MaxLength(10));
    }
}

[Validator]
public sealed partial class MakotoSignupValidator : Validator<Signup>
{
    protected override void Configure()
    {
        Property(x => x.Email).Require(x => x.IsEmail()).When(x => x is not null);
        Property(x => x.Name).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.Age).Require(x => x.Between(18, 120));
        Property(x => x.Address).ValidateWith<MakotoAddressValidator>();
        Each(x => x.Tags).Require(x => x.NotEmpty());
    }
}

public sealed class FluentAddressValidator : AbstractValidator<Address>
{
    public FluentAddressValidator()
    {
        RuleFor(x => x.City).NotEmpty();
        RuleFor(x => x.Zip).MaximumLength(10);
    }
}

public sealed class FluentSignupValidator : AbstractValidator<Signup>
{
    public FluentSignupValidator()
    {
        RuleFor(x => x.Email).EmailAddress().When(x => x.Email is not null);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Age).InclusiveBetween(18, 120);
        RuleFor(x => x.Address!).SetValidator(new FluentAddressValidator()).When(x => x.Address is not null);
        RuleForEach(x => x.Tags).NotEmpty();
    }
}

[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private static readonly Signup Valid = new("mike@example.com", "Mike", 30, new Address("Amsterdam", "1011AB"), ["a", "b", "c"]);
    private static readonly Signup Invalid = new("nope", "", 12, new Address("", "12345678901"), ["a", "", "c"]);

    private readonly MakotoSignupValidator _makoto = new();
    private readonly Makoto.IValidator<Signup> _makotoInterface = new MakotoSignupValidator();
    private readonly FluentSignupValidator _fluent = new();

    [Benchmark(Baseline = true)]
    public bool Fluent_valid() => _fluent.Validate(Valid).IsValid;

    [Benchmark]
    public bool Makoto_valid() => _makoto.Validate(Valid).IsValid;

    [Benchmark]
    public bool Makoto_valid_interface() => _makotoInterface.Validate(Valid).IsValid;

    [Benchmark]
    public bool Makoto_valid_static() => MakotoSignupValidator.Check(Valid);

    [Benchmark]
    public bool Fluent_invalid() => _fluent.Validate(Invalid).IsValid;

    [Benchmark]
    public bool Makoto_invalid() => _makoto.Validate(Invalid).IsValid;
}
