namespace Makoto.Tests.Validators;

/// <summary>Real validators, compiled by the generator running as an analyzer.</summary>
public class ValidatorTests
{
    private static readonly SignupValidator Validator = new();

    internal static Signup Valid() => new()
    {
        Email = "a@b.co",
        Password = "abc",
        PasswordRepeat = "abc",
        Age = 30,
        Address = new Address { City = "Oslo", Zip = "0150" },
        Home = new Coordinates(1, 2),
        Office = new Coordinates(1, 2),
        Tags = ["a", "b"],
        Items = [new LineItem { Name = "a", Quantity = 1 }],
        Extras = [new LineItem { Name = "b", Quantity = 2 }],
        Aliases = ["a"],
        Labels = ["b"],
    };

    private static (string Path, string Code, string Message)[] Failures(Signup signup)
    {
        var result = Validator.Validate(signup);

        Assert.Equal(result.IsValid, Validator.IsValid(signup));
        Assert.Equal(result.IsValid, SignupValidator.Check(signup));

        return result.Failures.ToArray().Select(f => (f.PropertyName, f.ErrorCode, f.Message)).ToArray();
    }

    private static string[] Paths(Signup signup) => Failures(signup).Select(f => f.Path).ToArray();

    private static Signup With(Action<Signup> change)
    {
        var signup = Valid();
        change(signup);
        return signup;
    }

    [Fact]
    public void Valid_model_passes()
    {
        Assert.Empty(Failures(Valid()));
    }

    [Fact]
    public void Same_project_rule_gives_its_code_and_message()
    {
        var signup = With(x => x.Email = "nope");

        Assert.Equal([("Email", "IsEmail", "'Email' is not an email address.")], Failures(signup));
    }

    [Fact]
    public void When_on_the_value_skips_the_require()
    {
        Assert.Empty(Failures(With(x => x.Email = null)));
    }

    [Fact]
    public void WithMessage_and_WithCode_override_the_defaults()
    {
        var signup = With(x => x.Email = $"a@b.{new string('c', 200)}");

        Assert.Equal([("Email", "TooLong", "Too long.")], Failures(signup));
    }

    [Fact]
    public void When_on_the_model_reads_the_model()
    {
        Assert.Empty(Failures(With(x => x.Company = "")));
        Assert.Equal(["Company"], Paths(With(x => (x.IsBusiness, x.Company) = (true, ""))));
    }

    [Fact]
    public void Cross_field_require_reads_the_model()
    {
        var signup = With(x => x.PasswordRepeat = "other");

        Assert.Equal([("PasswordRepeat", "Mismatch", "'PasswordRepeat' must match.")], Failures(signup));
    }

    [Fact]
    public void Named_replaces_the_path_and_fills_the_message()
    {
        var signup = With(x => x.Password = x.PasswordRepeat = "");

        Assert.Equal([("Secret", "Filled", "'Secret' must not be empty.")], Failures(signup));
    }

    [Fact]
    public void Constant_arguments_fill_the_message()
    {
        var signup = With(x => x.Password = x.PasswordRepeat = "longer");

        Assert.Equal([("Secret", "TooLong", "'Secret' must be at most 3 characters.")], Failures(signup));
    }

    [Fact]
    public void StopOnFailure_skips_the_rest_of_the_chain()
    {
        // Null fails Filled, and would fail Short as well if the chain carried on.
        var signup = With(x => x.Password = x.PasswordRepeat = null);

        Assert.Equal(["Secret"], Paths(signup));
    }

    [Fact]
    public void Generic_same_project_rule_gives_its_code()
    {
        var signup = With(x => x.Age = 151);

        Assert.Equal([("Age", "AtMost", "'Age' is not valid.")], Failures(signup));
    }

    [Fact]
    public void A_lambda_that_is_not_a_single_rule_call_gets_the_generic_defaults()
    {
        var signup = With(x => x.Nickname = "");

        Assert.Equal([("Nickname", "Require", "'Nickname' is not valid.")], Failures(signup));
    }

    [Fact]
    public void Nested_paths_are_prefixed()
    {
        var signup = With(x => x.Address = new Address { City = "", Zip = "too long" });

        Assert.Equal(
            [("Address.City", "Filled", "'City' must not be empty."), ("Address.Zip", "TooLong", "'Zip' must be at most 5 characters.")],
            Failures(signup));
    }

    [Fact]
    public void Null_nested_values_are_skipped()
    {
        Assert.Empty(Failures(With(x => x.Address = null)));
    }

    [Fact]
    public void Struct_values_and_struct_models_work()
    {
        Assert.Equal(["Home.Lat", "Home.Lon"], Paths(With(x => x.Home = new Coordinates(100, 100))));
        Assert.Equal(["Office.Lon"], Paths(With(x => x.Office = new Coordinates(5, 5))));
        Assert.Empty(Failures(With(x => x.Office = null)));
    }

    [Fact]
    public void Each_reports_the_index()
    {
        var signup = With(x => x.Tags = ["a", "", "c"]);

        Assert.Equal([("Tags[1]", "Filled", "'Tags' must not be empty.")], Failures(signup));
    }

    [Fact]
    public void Each_with_ValidateWith_reports_the_full_path()
    {
        var signup = With(x => x.Items =
        [
            new LineItem { Name = "a", Quantity = 1 },
            new LineItem { Name = "b", Quantity = 1 },
            new LineItem { Name = "c", Quantity = 1 },
            new LineItem { Name = "", Quantity = 0 },
        ]);

        Assert.Equal(
            [("Items[3].Name", "Filled", "'Name' must not be empty."), ("Items[3].Quantity", "IsPositive", "'Quantity' is not valid.")],
            Failures(signup));
    }

    [Fact]
    public void Each_stops_per_element_and_goes_on_with_the_next_one()
    {
        var signup = With(x => x.Extras = [null!, new LineItem { Name = "", Quantity = 1 }]);

        Assert.Equal(["Extras[0]", "Extras[1].Name"], Paths(signup));
    }

    [Fact]
    public void Each_over_any_enumerable_counts_elements()
    {
        Assert.Equal(["Aliases[2]"], Paths(With(x => x.Aliases = Enumerable.Range(0, 3).Select(i => new string('a', i * 3)))));
    }

    [Fact]
    public void Null_collections_are_skipped()
    {
        Assert.Empty(Failures(With(x => (x.Tags, x.Items, x.Extras, x.Aliases) = (null, null, null, null))));
    }

    [Fact]
    public void All_chains_report_in_declaration_order()
    {
        var signup = new Signup
        {
            Email = "nope",
            Password = "",
            PasswordRepeat = "x",
            Age = 200,
            Nickname = "",
            Address = new Address { City = "" },
            Home = new Coordinates(100, 0),
            Tags = [""],
            Items = [new LineItem { Name = "", Quantity = 1 }],
        };

        Assert.Equal(
            ["Email", "Secret", "PasswordRepeat", "Age", "Nickname", "Address.City", "Home.Lat", "Tags[0]", "Items[0].Name"],
            Paths(signup));
    }

    [Fact]
    public void Nulls_throw_for_reference_models()
    {
        Assert.Throws<ArgumentNullException>(() => Validator.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => Validator.IsValid(null!));
        Assert.Throws<ArgumentNullException>(() => SignupValidator.Check(null!));
    }

    [Fact]
    public void Throw_helper_reports_nested_paths()
    {
        var result = Validator.Validate(With(x => x.Address = new Address { City = "" }));

        var thrown = Assert.Throws<ValidationException>(() => result.ThrowIfInvalid());
        Assert.Equal("Address.City", Assert.Single(thrown.Failures).PropertyName);
    }

    [Fact]
    public void Failing_rules_share_one_failure_instance()
    {
        var signup = With(x => x.Email = "nope");

        Assert.Same(Validator.Validate(signup).Failures[0], Validator.Validate(signup).Failures[0]);
    }

    [Fact]
    public void Check_stops_at_the_first_failure()
    {
        var calls = 0;
        var signup = With(x =>
        {
            x.Email = "nope";
            x.Aliases = Enumerable.Repeat("a", 3).Select(a =>
            {
                calls++;
                return a;
            });
        });

        Assert.False(SignupValidator.Check(signup));
        Assert.Equal(0, calls);
    }
}
