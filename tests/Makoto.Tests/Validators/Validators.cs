namespace Makoto.Tests.Validators;

public sealed class Address
{
    public string? City { get; set; }

    public string? Zip { get; set; }
}

public sealed class LineItem
{
    public string? Name { get; set; }

    public int Quantity { get; set; }
}

public readonly record struct Coordinates(int Lat, int Lon);

public sealed class Signup
{
    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? PasswordRepeat { get; set; }

    public int Age { get; set; }

    public bool IsBusiness { get; set; }

    public string? Company { get; set; }

    public string? Nickname { get; set; }

    public Address? Address { get; set; }

    public Coordinates Home { get; set; }

    public Coordinates? Office { get; set; }

    public string[]? Tags { get; set; }

    public List<LineItem>? Items { get; set; }

    public LineItem[]? Extras { get; set; }

    public IEnumerable<string>? Aliases { get; set; }

    public IReadOnlyList<string>? Labels { get; set; }
}

// Local stand-ins: the built-in rules are written elsewhere and aren't available here.
[ValidationRule(Message = "'{PropertyName}' must not be empty.")]
public sealed class FilledRule : ValidationRule<string>
{
    public override bool Evaluate(string value) => value.Length != 0;
}

[ValidationRule(Message = "'{PropertyName}' must be at most {max} characters.", Code = "TooLong")]
public sealed class ShortRule : ValidationRule<string, int>
{
    public override bool Evaluate(string value, int max) => value.Length <= max;
}

[Validator]
public sealed partial class AddressValidator : Validator<Address>
{
    protected override void Configure()
    {
        Property(x => x.City).Require(x => x.Filled());
        Property(x => x.Zip).Require(x => x.Short(5)).When(x => x is not null);
    }
}

[Validator]
public sealed partial class LineItemValidator : Validator<LineItem>
{
    protected override void Configure()
    {
        Property(x => x.Name).Require(x => x.Filled());
        Property(x => x.Quantity).Require(x => x.IsPositive());
    }
}

[Validator]
public sealed partial class CoordinatesValidator : Validator<Coordinates>
{
    protected override void Configure()
    {
        Property(x => x.Lat).Require(x => x is >= -90 and <= 90).WithCode("Lat");
        Property(x => x.Lon).Require((model, x) => x is >= -180 and <= 180 && model.Lat != x).WithCode("Lon");
    }
}

[Validator]
public sealed partial class SignupValidator : Validator<Signup>
{
    private const int MaxEmail = 200;

    protected override void Configure()
    {
        Property(x => x.Email)
            .Require(x => x.IsEmail()).When(x => x is not null)
            .Require(x => x!.Length < MaxEmail).WithMessage("Too long.").WithCode("TooLong").When(x => x is not null);

        Property(x => x.Password)
            .Require(x => x.Filled())
            .Require(x => x.Short(3))
            .StopOnFailure()
            .Named("Secret");

        Property(x => x.PasswordRepeat)
            .Require((model, x) => model.Password == x).WithMessage("'{PropertyName}' must match.").WithCode("Mismatch");

        Property(x => x.Company)
            .Require(x => x.Filled()).When((model, _) => model.IsBusiness);

        Property(x => x.Age).Require(x => x.AtMost(150));

        Property(x => x.Nickname).Require(x => x is null || x.Filled());

        Property(x => x.Address).ValidateWith<AddressValidator>();
        Property(x => x.Home).ValidateWith<CoordinatesValidator>();
        Property(x => x.Office).ValidateWith<CoordinatesValidator>();

        Each(x => x.Tags).Require(x => x.Filled());
        Each(x => x.Items).ValidateWith<LineItemValidator>();
        Each(x => x.Extras).Require(x => x is not null).StopOnFailure().ValidateWith<LineItemValidator>();
        Each(x => x.Aliases).Require(x => x.Short(4));
        Each(x => x.Labels).Require(x => x.Filled());
    }
}
