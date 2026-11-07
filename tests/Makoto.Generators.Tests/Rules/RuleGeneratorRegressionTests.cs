namespace Makoto.Generators.Tests.Rules;

public class RuleGeneratorRegressionTests
{
    [Fact]
    public void Two_rules_with_the_same_class_name_in_different_namespaces() => RuleAssert.Compiles("""
        using Makoto;

        namespace One
        {
            [ValidationRule]
            public sealed class SameRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
        }

        namespace Two
        {
            [ValidationRule]
            public sealed class SameRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
        }
        """);

    [Fact]
    public void Generic_and_non_generic_rule_with_the_same_name() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule]
        public sealed class SameRule : ValidationRule<int>
        {
            public override bool Evaluate(int value) => true;
        }

        [ValidationRule]
        public sealed class SameRule<T> : ValidationRule<T>
        {
            public override bool Evaluate(T value) => true;
        }
        """);

    [Fact]
    public void Argument_named_value() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule]
        public sealed class EqualsToRule : ValidationRule<string, string>
        {
            public override bool Evaluate(string input, string value) => input == value;
        }
        """);

    [Fact]
    public void Argument_named_with_a_keyword() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule]
        public sealed class OrDefaultRule : ValidationRule<int, int>
        {
            public override bool Evaluate(int value, int @default) => value != @default;
        }
        """);

    [Fact]
    public void MKO003_sees_a_mutable_field_on_an_intermediate_base()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            public abstract class CountingBase : ValidationRule<int>
            {
                protected int Calls;
            }

            [ValidationRule]
            public sealed class CountingRule : CountingBase
            {
                public override bool Evaluate(int value) => Calls++ == 0;
            }
            """);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "MKO003");
    }

    [Fact]
    public void Argument_name_comes_from_an_Evaluate_override_on_an_intermediate_base()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class LimitRuleBase : ValidationRule<int, int>
            {
                public override bool Evaluate(int value, int limit) => value < limit;
            }

            [ValidationRule(Message = "Must be under {limit}.")]
            public sealed class UnderRule : LimitRuleBase;
            """);

        Assert.Contains("int limit", result.RuleSource("UnderRule.Rule.g.cs"));
    }

    [Fact]
    public void Nullable_class_constraint_is_kept()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class AnyRule<T> : ValidationRule<T>
                where T : class?
            {
                public override bool Evaluate(T value) => true;
            }
            """);

        Assert.Contains("where T : class?", result.RuleSource("AnyRule_1.Rule.g.cs"));
    }

    [Fact]
    public void Open_struct_rule_gets_a_nullable_overload()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class AlwaysRule<T> : ValidationRule<T>
                where T : struct
            {
                public override bool Evaluate(T value) => true;
            }

            public static class Caller
            {
                public static bool Call(int? value) => value.Always();
            }
            """);

        Assert.Contains("this T? value", result.RuleSource("AlwaysRule_1.Rule.g.cs"));
    }

    [Fact]
    public void Invalid_name_is_diagnosed()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule(Name = "not valid")]
            public sealed class BadNameRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        Assert.NotEmpty(result.GeneratorDiagnostics);
    }
}
