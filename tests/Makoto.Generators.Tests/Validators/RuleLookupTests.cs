namespace Makoto.Generators.Tests.Validators;

/// <summary>Where a Require's default code and message come from.</summary>
public class RuleLookupTests
{
    private const string Rules = """
        using Makoto;

        namespace Rules;

        [ValidationRule(Message = "'{PropertyName}' is over {max}.", Code = "TooLong")]
        public sealed class LongestRule : ValidationRule<string, int>
        {
            public override bool Evaluate(string value, int max) => value.Length <= max;
        }

        [ValidationRule(Message = "'{PropertyName}' is above {limit}.")]
        public sealed class AtMostRule<T> : ValidationRule<T, T>
            where T : System.IComparable<T>
        {
            public override bool Evaluate(T value, T limit) => value.CompareTo(limit) <= 0;
        }
        """;

    private const string Consumer = """
        using Makoto;
        using Rules;

        public sealed class M { public string? Name { get; set; } public int Age { get; set; } }

        public static class Samples { public static M Sample() => new() { Name = "long name", Age = 10 }; }

        [Validator]
        public sealed partial class V : Validator<M>
        {
            protected override void Configure()
            {
                Property(x => x.Name).Require(x => x.Longest(3));
                Property(x => x.Age).Require(x => x.AtMost(5)).WithCode("Old");
            }
        }
        """;

    [Fact]
    public void Rules_from_a_referenced_assembly_are_read_from_GeneratedRule()
    {
        var library = ValidatorHarness.CompileToReference(Rules);
        var result = ValidatorHarness.Run([library], Consumer);

        ValidatorHarness.AssertClean(result);

        var code = result.Generated("V");
        Assert.Contains("""new("Name", "TooLong", "'Name' is over 3.")""", code);
        Assert.Contains("""new("Age", "Old", "'Age' is above 5.")""", code);
    }

    [Fact]
    public void The_same_rules_give_the_same_defaults_in_the_same_project()
    {
        var result = GeneratorHarness.Run(Rules, Consumer);

        ValidatorHarness.AssertClean(result);

        var code = result.Generated("V");
        Assert.Contains("""new("Name", "TooLong", "'Name' is over 3.")""", code);
        Assert.Contains("""new("Age", "Old", "'Age' is above 5.")""", code);
    }

    [Fact]
    public void Validators_from_a_referenced_assembly_can_be_nested()
    {
        var library = ValidatorHarness.CompileToReference("""
            using Makoto;

            namespace Lib;

            public sealed class Inner { public int Value { get; set; } }

            [Validator]
            public sealed partial class InnerValidator : Validator<Inner>
            {
                protected override void Configure() => Property(x => x.Value).Require(x => x > 0);
            }
            """);

        var result = ValidatorHarness.Run([library], """
            using Makoto;
            using Lib;

            public sealed class Outer { public Inner Child { get; set; } = new(); }

            [Validator]
            public sealed partial class V : Validator<Outer>
            {
                protected override void Configure() => Property(x => x.Child).ValidateWith<InnerValidator>();
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains("global::Lib.InnerValidator.ValidateInto(value0, ref failures);", result.Generated("V"));
    }

    // RuleGenerator names its file after the class alone, so these only share their method name.
    private const string TwoNamespaces = """
        using Makoto;

        namespace First
        {
            [ValidationRule(Name = "Good", Code = "FirstCode", Message = "First.")]
            public sealed class FirstGoodRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => false;
            }
        }

        namespace Second
        {
            [ValidationRule(Name = "Good", Code = "SecondCode", Message = "Second.")]
            public sealed class SecondGoodRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => false;
            }
        }

        public sealed class M { public string? Name { get; set; } public int Age { get; set; } }
        """;

    [Theory]
    [InlineData("using First;", "", "FirstCode")]
    [InlineData("using Second;", "", "SecondCode")]
    [InlineData("", "namespace Second;", "SecondCode")]
    [InlineData("", "namespace Second.Deeper;", "SecondCode")]
    public void Same_named_rules_prefer_the_namespace_in_scope(string usings, string scope, string code)
    {
        var result = GeneratorHarness.Run(TwoNamespaces, $$"""
            using Makoto;
            {{usings}}
            {{scope}}

            [Validator]
            public sealed partial class V : Validator<M>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Good());
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains($"\"{code}\"", result.Sources.Single(s => s.Key.EndsWith("V.Validator.g.cs")).Value);
    }

    [Fact]
    public void A_global_using_puts_a_namespace_in_scope()
    {
        var result = GeneratorHarness.Run(TwoNamespaces, "global using Second;", """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<M>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Good());
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains("\"SecondCode\"", result.Generated("V"));
    }

    [Fact]
    public void Rule_overloads_in_one_namespace_are_told_apart_by_the_call()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule(Name = "Fine", Code = "Text")]
            public sealed class FineTextRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => false;
            }

            [ValidationRule(Name = "Fine", Code = "Number")]
            public sealed class FineNumberRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => false;
            }

            [ValidationRule(Name = "Fine", Code = "Limit")]
            public sealed class FineLimitRule : ValidationRule<int, int>
            {
                public override bool Evaluate(int value, int limit) => false;
            }

            public sealed class M { public string? Name { get; set; } public int Age { get; set; } public int? Maybe { get; set; } }

            [Validator]
            public sealed partial class V : Validator<M>
            {
                protected override void Configure()
                {
                    Property(x => x.Name).Require(x => x.Fine());
                    Property(x => x.Age).Require(x => x.Fine());
                    Property(x => x.Age).Require(x => x.Fine(1));
                    Property(x => x.Maybe).Require(x => x.Fine());
                }
            }
            """);

        ValidatorHarness.AssertClean(result);

        var code = result.Generated("V");
        Assert.Contains("""new("Name", "Text", """, code);
        Assert.Contains("""new("Age", "Number", """, code);
        Assert.Contains("""new("Age", "Limit", """, code);
        Assert.Contains("""new("Maybe", "Number", """, code);
    }

    [Fact]
    public void A_rule_with_errors_is_not_in_the_table()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule(Code = "Broken")]
            public class BrokenRule { }

            public sealed class M { public string? Name { get; set; } }

            [Validator]
            public sealed partial class V : Validator<M>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Broken());
            }
            """);

        Assert.Contains("MKO001", result.Ids());
        Assert.Contains("""new("Name", "Require", "'Name' is not valid.")""", result.Generated("V"));
    }

    [Fact]
    public void An_unknown_call_falls_back_and_leaves_the_error_to_the_compiler()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            public sealed class M { public string? Name { get; set; } }

            [Validator]
            public sealed partial class V : Validator<M>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Nonsense());
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Equal(["CS1061"], result.CompileErrors.Select(e => e.Id).Distinct());
    }
}
