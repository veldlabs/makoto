namespace Makoto.Generators.Tests.Rules;

public class RuleDiagnosticTests
{
    [Fact]
    public void MKO001_no_rule_base() => RuleAssert.Rejected("MKO001", """
        using Makoto;

        [ValidationRule]
        public sealed class NotARule;
        """, "'NotARule'");

    [Fact]
    public void MKO001_a_lookalike_base_does_not_count() => RuleAssert.Rejected("MKO001", """
        using Makoto;

        namespace Other
        {
            public abstract class ValidationRule<T>
            {
                public abstract bool Evaluate(T value);
            }

            [ValidationRule]
            public sealed class LookalikeRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
        }
        """, "'LookalikeRule'");

    [Fact]
    public void MKO002_neither_sealed_nor_partial() => RuleAssert.Rejected("MKO002", """
        using Makoto;

        [ValidationRule]
        public class OpenRule : ValidationRule<int>
        {
            public override bool Evaluate(int value) => true;
        }
        """, "'OpenRule'");

    [Fact]
    public void MKO003_mutable_field() => RuleAssert.Rejected("MKO003", """
        using Makoto;

        [ValidationRule]
        public sealed class CountingRule : ValidationRule<int>
        {
            private int _calls;

            public override bool Evaluate(int value) => _calls++ == 0;
        }
        """, "'_calls'");

    [Fact]
    public void MKO003_auto_property_with_a_setter() => RuleAssert.Rejected("MKO003", """
        using Makoto;

        [ValidationRule]
        public sealed class ConfiguredRule : ValidationRule<int>
        {
            public int Limit { get; set; }

            public override bool Evaluate(int value) => value < Limit;
        }
        """, "'ConfiguredRule'");

    [Fact]
    public void MKO003_is_reported_on_every_mutable_field()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule]
            public sealed class CountingRule : ValidationRule<int>
            {
                private int _first;
                private int _second;

                public override bool Evaluate(int value) => _first++ == _second++;
            }
            """);

        Assert.Equal(["MKO003", "MKO003"], result.GeneratorDiagnostics.Select(d => d.Id));
    }

    [Fact]
    public void MKO003_allows_readonly_static_and_const_fields() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule]
        public sealed class StatelessRule : ValidationRule<int>
        {
            private const int Limit = 1;
            private static int s_calls;
            private readonly int _offset = 1;

            public override bool Evaluate(int value) => value + _offset + s_calls++ > Limit;
        }
        """);

    [Fact]
    public void MKO004_private_constructor() => RuleAssert.Rejected("MKO004", """
        using Makoto;

        [ValidationRule]
        public sealed class HiddenRule : ValidationRule<int>
        {
            private HiddenRule()
            {
            }

            public override bool Evaluate(int value) => true;
        }
        """, "'HiddenRule'");

    [Fact]
    public void MKO004_constructor_with_parameters() => RuleAssert.Rejected("MKO004", """
        using Makoto;

        [ValidationRule]
        public sealed class ArgumentRule : ValidationRule<int>
        {
            private readonly int _limit;

            public ArgumentRule(int limit) => _limit = limit;

            public override bool Evaluate(int value) => value < _limit;
        }
        """, "'ArgumentRule'");

    [Fact]
    public void MKO004_allows_an_internal_constructor() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule]
        public sealed class InternalCtorRule : ValidationRule<int>
        {
            internal InternalCtorRule()
            {
            }

            public override bool Evaluate(int value) => true;
        }
        """);

    // Caught by accident: an abstract class's implicit constructor is protected. The message is about the constructor.
    [Fact]
    public void MKO004_abstract_partial_rule() => RuleAssert.Rejected("MKO004", """
        using Makoto;

        [ValidationRule]
        public abstract partial class BaseRule : ValidationRule<int>;
        """, "'BaseRule'");

    [Fact]
    public void MKO005_nested_rule() => RuleAssert.Rejected("MKO005", """
        using Makoto;

        public static class Outer
        {
            [ValidationRule]
            public sealed class NestedRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
        }
        """, "'NestedRule'");

    [Fact]
    public void MKO006_invalid_name() => RuleAssert.Rejected("MKO006", """
        using Makoto;

        [ValidationRule(Name = "not valid")]
        public sealed class BadNameRule : ValidationRule<int>
        {
            public override bool Evaluate(int value) => true;
        }
        """, "'not valid'");

    // IsValidIdentifier accepts reserved keywords, which would emit 'public static bool class(...)'.
    [Fact]
    public void MKO006_reserved_keyword_name() => RuleAssert.Rejected("MKO006", """
        using Makoto;

        [ValidationRule(Name = "class")]
        public sealed class ClassRule : ValidationRule<int>
        {
            public override bool Evaluate(int value) => true;
        }
        """, "'class'");

    [Fact]
    public void MKO006_allows_a_contextual_keyword_name() => RuleAssert.Compiles("""
        using Makoto;

        [ValidationRule(Name = "value")]
        public sealed class ValueRule : ValidationRule<int>
        {
            public override bool Evaluate(int value) => true;
        }

        public static class Caller
        {
            public static bool Call(int number) => number.value();
        }
        """);

    [Fact]
    public void Every_problem_of_a_rule_is_reported_at_once()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            public static class Outer
            {
                [ValidationRule]
                public class BadRule : ValidationRule<int>
                {
                    private int _calls;

                    public BadRule(int calls) => _calls = calls;

                    public override bool Evaluate(int value) => true;
                }
            }
            """);

        Assert.Equal(["MKO002", "MKO003", "MKO004", "MKO005"], result.GeneratorDiagnostics.Select(d => d.Id).Order());
        Assert.Empty(result.Sources);
    }

    [Fact]
    public void Diagnostics_point_at_the_class_name()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule]
            public class OpenRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        var position = Assert.Single(result.GeneratorDiagnostics).Location.GetLineSpan().StartLinePosition;
        Assert.Equal((3, 13), (position.Line, position.Character));
    }

    [Fact]
    public void One_bad_rule_does_not_stop_the_others()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule]
            public class OpenRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }

            [ValidationRule]
            public sealed class GoodRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        Assert.Equal("MKO002", Assert.Single(result.GeneratorDiagnostics).Id);
        Assert.Equal("GoodRule.Rule.g.cs", Assert.Single(result.Sources).Key);
    }
}
