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

    // An Evaluate overload declared before the real override used to be picked, and indexing the base
    // method's parameters with the overload's count crashed both generators.
    [Fact]
    public void Evaluate_overload_declared_before_the_override_is_skipped()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class Mid : ValidationRule<string>
            {
                public abstract bool Evaluate(string v, int n);
            }

            [ValidationRule]
            public sealed class R2 : Mid
            {
                public override bool Evaluate(string v, int n) => true;
                public override bool Evaluate(string v) => true;
            }
            """);

        var extension = RuleAssert.Extension(result.Load(), "R2Extensions", "R2");
        Assert.Equal([typeof(string)], extension.GetParameters().Select(p => p.ParameterType));
    }

    [Fact]
    public void Rule_in_a_keyword_namespace()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            namespace @event;

            [ValidationRule]
            public sealed class R : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }

            public static class Caller
            {
                public static bool Call(string value) => value.R();
            }
            """);

        Assert.Equal("event.R.Rule.g.cs", Assert.Single(result.Sources).Key);
        Assert.Contains("namespace @event;", result.RuleSource("R.Rule.g.cs"));
    }

    [Fact]
    public void Rule_with_a_keyword_class_name()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Name = "IsEvent")]
            public partial class @event : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }

            public static class Caller
            {
                public static bool Call(string value) => value.IsEvent();
            }
            """);

        Assert.Contains("sealed partial class @event;", result.RuleSource("event.Rule.g.cs"));
        Assert.True(result.Load().GetType("event")!.IsSealed);
    }
}
