namespace Makoto.Generators.Tests.Validators;

/// <summary>Require, When, the other modifiers, messages and how lambdas get copied. Everything here runs.</summary>
public class ChainBehaviorTests
{
    private const string Prelude = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Makoto;

        public sealed class Person
        {
            public string? Name { get; set; }
            public string? Nick { get; set; }
            public int Age { get; set; }
            public bool IsAdult { get; set; }
            public Person? Friend { get; set; }
        }

        [ValidationRule(Message = "'{PropertyName}' is not an email address.")]
        public partial class IsEmailValidationRule : ValidationRule<string>
        {
            public override bool Evaluate(string value) => value.Contains('@');
        }

        [ValidationRule(Message = "'{PropertyName}' is over {max}.", Code = "TooLong")]
        public sealed class LongestRule : ValidationRule<string, int>
        {
            public override bool Evaluate(string value, int max) => value.Length <= max;
        }

        [ValidationRule(Message = "'{PropertyName}' is not within {low}..{high}.")]
        public sealed class WithinRule : ValidationRule<int, int, int>
        {
            public override bool Evaluate(int value, int low, int high) => value >= low && value <= high;
        }

        [ValidationRule(Message = "'{PropertyName}' is not '{text}'.")]
        public sealed class IsTextRule : ValidationRule<string, string>
        {
            public override bool Evaluate(string value, string text) => value == text;
        }

        public static class Samples
        {
            public static Person Empty() => new();
            public static Person Valid() => new() { Name = "a@b", Nick = "a@b", Age = 30, IsAdult = true };
            public static Person NoAt() => new() { Name = "nope", Nick = "nope", Age = 30, IsAdult = true };
            public static Person Child() => new() { Name = "nope", Age = 7 };
            public static Person Long() => new() { Name = "much too long for this", Age = 200 };
        }
        """;

    private static System.Reflection.Assembly Validator(string configure, string members = "") =>
        ValidatorHarness.Load(Prelude, $$"""
            using System;
            using System.Linq;
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Person>
            {
                {{members}}

                protected override void Configure()
                {
                    {{configure}}
                }
            }
            """);

    [Fact]
    public void Same_project_rule_supplies_code_and_message()
    {
        var assembly = Validator("Property(x => x.Name).Require(x => x.IsEmail());");

        Assert.Equal(["Name|IsEmail|'Name' is not an email address."], assembly.Failures("V", "NoAt"));
        Assert.Empty(assembly.Failures("V", "Valid"));
    }

    [Fact]
    public void Rule_code_comes_from_the_attribute()
    {
        var assembly = Validator("Property(x => x.Name).Require(x => x.Longest(5));");

        Assert.Equal(["Name|TooLong|'Name' is over 5."], assembly.Failures("V", "Long"));
    }

    [Fact]
    public void A_lambda_that_is_more_than_one_rule_call_gets_the_generic_defaults()
    {
        var assembly = Validator("""
            Property(x => x.Name).Require(x => x.IsEmail() || x.Longest(1));
            Property(x => x.Nick).Require(x => !x.IsEmail());
            Property(x => x.Age).Require(x => x > 18);
            """);

        Assert.Equal(
            ["Name|Require|'Name' is not valid.", "Age|Require|'Age' is not valid."],
            assembly.Failures("V", "Child"));
    }

    [Theory]
    [InlineData("x => { return x.IsEmail(); }")]
    [InlineData("x => (x.IsEmail())")]
    [InlineData("x => x!.IsEmail()")]
    [InlineData("(m, x) => m.Name.IsEmail()")]
    [InlineData("x => IsEmailValidationRuleExtensions.IsEmail(x)")]
    public void Only_exactly_param_dot_rule_counts_as_a_rule_call(string lambda)
    {
        var assembly = Validator($"Property(x => x.Name).Require({lambda});");

        Assert.Equal(["Name|Require|'Name' is not valid."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void A_method_that_binds_but_is_no_rule_gets_the_generic_defaults()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.StartsWith("a"));""");

        Assert.Equal(["Name|Require|'Name' is not valid."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void WithMessage_and_WithCode_replace_only_what_they_name()
    {
        var assembly = Validator("""
            Property(x => x.Name).Require(x => x.IsEmail()).WithCode("Mail");
            Property(x => x.Nick).Require(x => x.IsEmail()).WithMessage("Bad {PropertyName}!");
            """);

        Assert.Equal(
            ["Name|Mail|'Name' is not an email address.", "Nick|IsEmail|Bad Nick!"],
            assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Modifiers_read_constants_after_a_require_that_does_not_bind()
    {
        var assembly = Validator(
            "Property(x => x.Name).Require(x => x.IsEmail()).WithMessage(Text).WithCode(Prefix + \"Code\").Named(Prefix);",
            """
            private const string Text = "Constant message.";
            private const string Prefix = "Pre";
            """);

        Assert.Equal(["Pre|PreCode|Constant message."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Modifiers_apply_to_the_require_before_them()
    {
        var assembly = Validator("""
            Property(x => x.Name)
                .Require(x => x.IsEmail()).WithCode("First")
                .Require(x => x.Longest(2)).WithCode("Second").WithMessage("Two.");
            """);

        Assert.Equal(
            ["Name|First|'Name' is not an email address.", "Name|Second|Two."],
            assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void The_last_of_a_repeated_modifier_wins()
    {
        var assembly = Validator("""
            Property(x => x.Name).Named("A").Require(x => x.IsEmail()).WithCode("X").WithCode("Y").Named("B");
            """);

        Assert.Equal(["B|Y|'B' is not an email address."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void When_gets_the_value()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.IsEmail()).When(x => x is not null);""");

        Assert.Empty(assembly.Failures("V", "Empty"));
        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void When_gets_the_model()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.IsEmail()).When((model, x) => model.IsAdult);""");

        Assert.Empty(assembly.Failures("V", "Child"));
        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Several_whens_are_anded_and_only_guard_their_own_require()
    {
        var assembly = Validator("""
            Property(x => x.Name)
                .Require(x => x.Longest(1)).WithCode("A").When(x => x is not null).When((m, _) => m.IsAdult)
                .Require(x => x.Longest(2)).WithCode("B");
            """);

        Assert.Equal(["Name|B|'Name' is over 2."], assembly.Failures("V", "Child"));
        Assert.Equal(2, assembly.Failures("V", "NoAt").Length);
    }

    [Fact]
    public void Require_can_read_the_model()
    {
        var assembly = Validator("""Property(x => x.Age).Require((model, age) => model.IsAdult == age >= 18).WithCode("Cross");""");

        Assert.Empty(assembly.Failures("V", "Valid"));
        Assert.Equal(["Age|Cross|'Age' is not valid."], assembly.Failures("V", "Long"));
    }

    [Fact]
    public void A_rule_call_on_the_value_of_a_two_parameter_lambda_is_a_rule_call()
    {
        var assembly = Validator("Property(x => x.Name).Require((model, x) => x.IsEmail());");

        Assert.Equal(["Name|IsEmail|'Name' is not an email address."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Constant_arguments_fill_the_message()
    {
        var assembly = Validator(
            """
            Property(x => x.Name).Require(x => x.IsText("q'" + Suffix));
            Property(x => x.Age).Require(x => x.Within(Low, Low + 1));
            """,
            """
            private const string Suffix = "z";
            private const int Low = 1;
            """);

        Assert.Equal(
            ["Name|IsText|'Name' is not 'q'z'.", "Age|Within|'Age' is not within 1..2."],
            assembly.Failures("V", "Long"));
    }

    [Fact]
    public void Named_arguments_fill_the_right_placeholders()
    {
        var assembly = Validator("Property(x => x.Age).Require(x => x.Within(high: 2, low: 1));");

        Assert.Equal(["Age|Within|'Age' is not within 1..2."], assembly.Failures("V", "Long"));
    }

    [Fact]
    public void Arguments_that_are_not_constant_stay_as_placeholders()
    {
        var assembly = Validator(
            "Property(x => x.Age).Require(x => x.Within(1, Limit));",
            "private static readonly int Limit = 2;");

        Assert.Equal(["Age|Within|'Age' is not within 1..{high}."], assembly.Failures("V", "Long"));
    }

    [Fact]
    public void Arguments_fill_a_message_from_WithMessage_too()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.Longest(3)).WithMessage("At most {max}.");""");

        Assert.Equal(["Name|TooLong|At most 3."], assembly.Failures("V", "Long"));
    }

    [Fact]
    public void Named_changes_the_path_and_the_message_wherever_it_appears()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.IsEmail()).Named("Login");""");

        Assert.Equal(["Login|IsEmail|'Login' is not an email address."], assembly.Failures("V", "NoAt"));
    }

    [Theory]
    [InlineData("""Property(x => x.Name).StopOnFailure().Require(x => x.IsEmail()).Require(x => x.Longest(1));""")]
    [InlineData("""Property(x => x.Name).Require(x => x.IsEmail()).StopOnFailure().Require(x => x.Longest(1));""")]
    [InlineData("""Property(x => x.Name).Require(x => x.IsEmail()).Require(x => x.Longest(1)).StopOnFailure();""")]
    public void StopOnFailure_applies_to_the_chain_wherever_it_appears(string configure)
    {
        Assert.Single(Validator(configure).Failures("V", "NoAt"));
    }

    [Fact]
    public void StopOnFailure_skips_later_steps_that_would_throw()
    {
        var assembly = Validator("""
            Property(x => x.Name)
                .Require(x => x is not null).StopOnFailure()
                .Require(x => x!.Length > 100);
            """);

        Assert.Equal(["Name|Require|'Name' is not valid."], assembly.Failures("V", "Empty"));
    }

    [Fact]
    public void StopOnFailure_only_stops_its_own_chain()
    {
        var assembly = Validator("""
            Property(x => x.Name).Require(x => x.IsEmail()).Require(x => x.Longest(1)).StopOnFailure();
            Property(x => x.Nick).Require(x => x.IsEmail());
            """);

        Assert.Equal(["Name", "Nick"], assembly.Paths("V", "NoAt"));
    }

    [Fact]
    public void A_when_that_is_false_does_not_count_as_a_failure_for_StopOnFailure()
    {
        var assembly = Validator("""
            Property(x => x.Name)
                .Require(x => x.IsEmail()).When(x => false)
                .Require(x => x.Longest(1)).StopOnFailure();
            """);

        Assert.Equal(["Name|TooLong|'Name' is over 1."], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Failures_keep_declaration_order()
    {
        var assembly = Validator("""
            Property(x => x.Nick).Require(x => x.IsEmail());
            Property(x => x.Name).Require(x => x.IsEmail());
            Property(x => x.Age).Require(x => x < 0);
            """);

        Assert.Equal(["Nick", "Name", "Age"], assembly.Paths("V", "NoAt"));
    }

    [Theory]
    [InlineData("x => x.Friend!.Name", "Friend.Name")]
    [InlineData("x => x.Friend?.Name", "Friend.Name")]
    [InlineData("x => x.Friend?.Friend?.Name", "Friend.Friend.Name")]
    [InlineData("x => x.Friend!.Friend!.Name!", "Friend.Friend.Name")]
    [InlineData("x => (x.Friend!).Name", "Friend.Name")]
    [InlineData("static x => x.Name", "Name")]
    [InlineData("(x) => x.Name", "Name")]
    [InlineData("(Person x) => x.Name", "Name")]
    [InlineData("static (Person p) => p.Name", "Name")]
    [InlineData("@class => @class.Name", "Name")]
    [InlineData("_ => _.Name", "Name")]
    public void Selectors_report_the_member_path(string selector, string path)
    {
        var result = GeneratorHarness.Run(Prelude, $$"""
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Person>
            {
                protected override void Configure() => Property({{selector}}).Require(x => false);
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains($"new(\"{path}\", \"Require\", \"'{path}' is not valid.\")", result.Generated("V"));
    }

    [Theory]
    [InlineData("x => x.IsEmail()")]
    [InlineData("(x) => x.IsEmail()")]
    [InlineData("(string? x) => x.IsEmail()")]
    [InlineData("static x => x.IsEmail()")]
    [InlineData("static (string? x) => x.IsEmail()")]
    [InlineData("(_, x) => x.IsEmail()")]
    [InlineData("(Person m, string? x) => x.IsEmail()")]
    public void Lambda_forms_all_copy(string lambda)
    {
        Assert.Equal(["Name|IsEmail|'Name' is not an email address."], Validator($"Property(x => x.Name).Require({lambda});").Failures("V", "NoAt"));
    }

    [Fact]
    public void Block_bodies_copy()
    {
        var assembly = Validator("""
            Property(x => x.Age).Require(age =>
            {
                var doubled = age * 2;
                return doubled > 100;
            }).When((m, age) =>
            {
                if (m.Name is null) return false;
                return age > 0;
            });
            """);

        Assert.Empty(assembly.Failures("V", "Empty"));
        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Several_discards_become_separate_parameters()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.IsEmail()).When((_, _) => false);""");

        Assert.Empty(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void A_single_underscore_is_a_real_parameter()
    {
        var assembly = Validator("""Property(x => x.Age).Require(_ => _ > 100);""");

        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Nested_lambdas_and_their_locals_are_not_captures()
    {
        var assembly = Validator("""
            Property(x => x.Name).Require(x =>
            {
                var bad = '!';
                return x is null || x.All(c => c != bad && Func(y => y != x.Length)(c));
            });
            """,
            "private static Func<char, bool> Func(Func<int, bool> f) => c => f(c);");

        Assert.Empty(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Static_members_of_the_validator_can_be_used()
    {
        var assembly = Validator(
            "Property(x => x.Age).Require(x => IsAllowed(x, Ceiling) && Names.Length == Count);",
            """
            private const int Ceiling = 100;
            private static readonly string[] Names = ["a"];
            private static int Count => 1;
            private static bool IsAllowed(int age, int ceiling) => age < ceiling;
            """);

        Assert.Empty(assembly.Failures("V", "NoAt"));
        Assert.Single(assembly.Failures("V", "Long"));
    }

    [Fact]
    public void Object_initializers_and_named_arguments_in_lambdas_are_not_captures()
    {
        var assembly = Validator(
            """Property(x => x.Age).Require(x => new Person { Age = x, Name = nameof(Instance) }.Age == Same(value: x));""",
            """
            private int Instance => 0;
            private static int Same(int value) => value;
            """);

        Assert.Empty(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Property_patterns_and_anonymous_types_in_lambdas_are_not_captures()
    {
        var assembly = Validator(
            """Property(x => x.Name).Require(x => x is { Length: > 0 } && new { Instance = x }.Instance is not null && (Instance: 1, 2).Instance == 1);""",
            "private int Instance => 0;");

        Assert.Empty(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void An_expression_bodied_configure_works()
    {
        var assembly = ValidatorHarness.Load(Prelude, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Person>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.IsEmail());
            }
            """);

        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void This_qualified_roots_work()
    {
        var assembly = Validator("this.Property(x => x.Name).Require(x => x.IsEmail());");

        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void Explicit_type_arguments_on_the_root_work()
    {
        var assembly = Validator("Property<string?>(x => x.Name).Require(x => x.IsEmail());");

        Assert.Single(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void A_chain_without_steps_and_an_empty_configure_are_fine()
    {
        var assembly = Validator("Property(x => x.Name); Property(x => x.Nick).StopOnFailure();");

        Assert.Empty(assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void A_null_model_throws_from_every_entry_point()
    {
        var assembly = Validator("Property(x => x.Name).Require(x => x.IsEmail());");
        var type = assembly.GetType("V")!;
        var validator = Activator.CreateInstance(type);

        foreach (var (method, target) in new[] { ("Validate", validator), ("IsValid", validator), ("Check", null) })
        {
            var thrown = Assert.Throws<System.Reflection.TargetInvocationException>(() => type.GetMethod(method)!.Invoke(target, [null]));
            Assert.IsType<ArgumentNullException>(thrown.InnerException);
        }
    }

    [Fact]
    public void Empty_validator_passes()
    {
        var assembly = Validator("");

        Assert.Empty(assembly.Failures("V", "Empty"));
    }

    [Fact]
    public void Message_text_is_escaped()
    {
        var assembly = Validator("""Property(x => x.Name).Require(x => x.IsEmail()).WithMessage("a \"quoted\"\nline \\ {PropertyName}");""");

        Assert.Equal(["Name|IsEmail|a \"quoted\"\nline \\ Name"], assembly.Failures("V", "NoAt"));
    }

    [Fact]
    public void A_comment_inside_a_lambda_survives_the_copy()
    {
        var assembly = Validator("""
            Property(x => x.Age).Require(x => x > 100 // over a hundred
                || x < 0);
            """);

        Assert.Single(assembly.Failures("V", "NoAt"));
    }
}
