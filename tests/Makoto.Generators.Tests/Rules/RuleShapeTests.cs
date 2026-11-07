using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Makoto.Generators.Tests.Rules;

public class RuleShapeTests
{
    [Fact]
    public void Partial_rule_gets_sealed()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            namespace Demo;

            [ValidationRule]
            public partial class PartialRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        Assert.Contains("sealed partial class PartialRule;", result.RuleSource("PartialRule.Rule.g.cs"));
        Assert.True(result.Load().GetType("Demo.PartialRule")!.IsSealed);
    }

    [Fact]
    public void Generic_partial_rule_gets_sealed()
    {
        var result = RuleAssert.Compiles("""
            using System;
            using Makoto;

            namespace Demo;

            [ValidationRule]
            public partial class PartialRule<T> : ValidationRule<T>
                where T : IComparable<T>
            {
                public override bool Evaluate(T value) => true;
            }
            """);

        Assert.Contains("sealed partial class PartialRule<T>;", result.RuleSource("PartialRule_1.Rule.g.cs"));
        Assert.True(result.Load().GetType("Demo.PartialRule`1")!.IsSealed);
    }

    [Fact]
    public void Global_partial_rule_gets_sealed()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public partial class PartialRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        Assert.True(result.Load().GetType("PartialRule")!.IsSealed);
    }

    [Theory]
    [InlineData("public sealed partial class SealedRule : ValidationRule<int> { public override bool Evaluate(int value) => true; }", "")]
    [InlineData("public partial class SealedRule : ValidationRule<int> { public override bool Evaluate(int value) => true; }", "public sealed partial class SealedRule;")]
    public void Already_sealed_partial_rule_is_left_alone(string declaration, string otherPart)
    {
        var result = RuleAssert.Compiles($$"""
            using Makoto;

            [ValidationRule]
            {{declaration}}
            {{otherPart}}
            """);

        Assert.DoesNotContain("sealed partial", result.RuleSource("SealedRule.Rule.g.cs"));
    }

    [Fact]
    public void Partial_rule_split_over_two_files()
    {
        var result = RuleAssert.Compiles(
            """
            using Makoto;

            [ValidationRule]
            public partial class SplitRule : ValidationRule<int>
            {
            }
            """,
            """
            public partial class SplitRule
            {
                public override bool Evaluate(int value) => value == 1;
            }
            """);

        Assert.True(result.Load().GetType("SplitRule")!.IsSealed);
    }

    [Fact]
    public void Internal_rule_gets_an_internal_extension()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            namespace Demo;

            internal sealed class Secret;

            [ValidationRule]
            internal sealed class HiddenRule : ValidationRule<Secret>
            {
                public override bool Evaluate(Secret value) => true;
            }

            public static class Caller
            {
                public static bool Call() => new Secret().Hidden();
            }
            """);

        Assert.Contains("internal static partial class HiddenRuleExtensions", result.RuleSource("HiddenRule.Rule.g.cs"));

        var assembly = result.Load();
        Assert.False(assembly.GetType("Demo.HiddenRuleExtensions")!.IsPublic);
        Assert.Equal(true, assembly.GetType("Demo.Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void Class_without_an_access_modifier_is_internal()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            sealed class DefaultAccessRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        Assert.Contains("internal static partial class DefaultAccessRuleExtensions", result.RuleSource("DefaultAccessRule.Rule.g.cs"));
    }

    [Fact]
    public void Rule_in_the_global_namespace()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class GlobalRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => value == 1;
            }

            [ValidationRule]
            public sealed class GlobalGenericRule<T> : ValidationRule<T>
            {
                public override bool Evaluate(T value) => true;
            }

            public static class Caller
            {
                public static bool Call() => 1.Global() && 1.GlobalGeneric();
            }
            """);

        Assert.DoesNotContain("namespace", result.RuleSource("GlobalRule.Rule.g.cs"));
        Assert.Contains("private static readonly global::GlobalRule s_rule", result.RuleSource("GlobalRule.Rule.g.cs"));
        Assert.Contains("typeof(global::GlobalGenericRule<>)", result.RuleSource("GlobalGenericRule_1.Rule.g.cs"));
        Assert.Equal(true, result.Load().GetType("Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void Rule_in_a_nested_namespace()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            namespace Demo.Deep.Down
            {
                [ValidationRule]
                public sealed class DeepRule : ValidationRule<int>
                {
                    public override bool Evaluate(int value) => true;
                }
            }
            """);

        Assert.NotNull(result.Load().GetType("Demo.Deep.Down.DeepRuleExtensions"));
    }

    private const string ConstraintFixture = """
        public interface IThing
        {
            int Id { get; }
        }

        public sealed class Thing : IThing
        {
            public int Id => 1;
        }

        public struct Spot : IThing
        {
            public int Id => 2;
        }
        """;

    [Theory]
    [InlineData("class", "class", typeof(string))]
    [InlineData("struct", "struct", typeof(int))]
    [InlineData("notnull", "notnull", typeof(int))]
    [InlineData("new()", "new()", typeof(object))]
    [InlineData("unmanaged", "unmanaged", typeof(int))]
    [InlineData("System.Enum", "global::System.Enum", typeof(DayOfWeek))]
    [InlineData("System.IComparable<T>", "global::System.IComparable<T>", typeof(int))]
    [InlineData("struct, System.Enum", "struct, global::System.Enum", typeof(DayOfWeek))]
    [InlineData("class, new()", "class, new()", typeof(object))]
    [InlineData("notnull, System.IComparable<T>", "notnull, global::System.IComparable<T>", typeof(int))]
    [InlineData("class, System.IComparable<T>, new()", "class, global::System.IComparable<T>, new()", typeof(Version))]
    public void Constraint_is_repeated_on_the_holder_and_the_method(string constraint, string emitted, Type typeArgument)
    {
        var result = RuleAssert.Compiles($$"""
            using Makoto;

            namespace Demo;

            [ValidationRule]
            public sealed class ConstrainedRule<T> : ValidationRule<T>
                where T : {{constraint}}
            {
                public override bool Evaluate(T value) => true;
            }
            """);

        var source = result.RuleSource("ConstrainedRule_1.Rule.g.cs");
        Assert.Contains($"private static class Holder<T> where T : {emitted}", source);
        Assert.Contains($"(this T value) where T : {emitted} =>", source);

        var method = RuleAssert.Extension(result.Load(), "Demo.ConstrainedRuleExtensions", "Constrained", typeArgument);
        var sample = typeArgument == typeof(string) ? "" : Activator.CreateInstance(typeArgument);
        Assert.Equal(true, method.Invoke(null, [sample]));
    }

    [Fact]
    public void Interface_constraint_from_the_same_compilation()
    {
        var result = RuleAssert.Compiles($$"""
            using Makoto;

            namespace Demo;

            {{ConstraintFixture}}

            [ValidationRule]
            public sealed class HasIdRule<T> : ValidationRule<T>
                where T : IThing
            {
                public override bool Evaluate(T value) => value.Id != 0;
            }

            public static class Caller
            {
                public static bool Call() => new Thing().HasId() && new Spot().HasId();
            }
            """);

        Assert.Contains("where T : global::Demo.IThing", result.RuleSource("HasIdRule_1.Rule.g.cs"));
        Assert.Equal(true, result.Load().GetType("Demo.Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void New_constraint_is_usable_inside_the_rule()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;
            using System.Collections.Generic;

            [ValidationRule]
            public sealed class SameAsNewRule<T> : ValidationRule<T>
                where T : new()
            {
                public override bool Evaluate(T value) => EqualityComparer<T>.Default.Equals(value, new T());
            }

            public static class Caller
            {
                public static bool Call() => 0.SameAsNew();
            }
            """);

        Assert.Equal(true, result.Load().GetType("Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void Several_type_parameters_with_their_own_constraints()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class PairRule<T, TOther, TFree> : ValidationRule<T, TOther, TFree>
                where T : class
                where TOther : struct
            {
                public override bool Evaluate(T value, TOther other, TFree free) => true;
            }

            public static class Caller
            {
                public static bool Call() => "a".Pair(1, 1.5);
            }
            """);

        var source = result.RuleSource("PairRule_3.Rule.g.cs");
        Assert.Contains("Holder<T, TOther, TFree> where T : class where TOther : struct", source);
        Assert.Contains("typeof(global::PairRule<,,>)", source);
        Assert.Equal(true, result.Load().GetType("Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void Nullable_twin_of_a_struct_rule_shares_the_name_with_the_open_rule()
    {
        var result = RuleAssert.Compiles("""
            using System;
            using Makoto;

            [ValidationRule]
            public sealed class AboveRule<T> : ValidationRule<T, T>
                where T : IComparable<T>
            {
                public override bool Evaluate(T value, T limit) => value.CompareTo(limit) > 0;
            }

            [ValidationRule(Name = "Above")]
            public sealed class AboveNullableRule<T> : ValidationRule<T?, T>
                where T : struct, IComparable<T>
            {
                public override bool Evaluate(T? value, T limit) => value is { } v && v.CompareTo(limit) > 0;
            }

            public static class Caller
            {
                public static bool Call(int value, int? nullable) => value.Above(1) && nullable.Above(1);
            }
            """);

        Assert.Contains("Above<T>(this T? value, T limit)", result.RuleSource("AboveNullableRule_1.Rule.g.cs"));
        Assert.Equal(true, result.Load().GetType("Caller")!.GetMethod("Call")!.Invoke(null, [2, 2]));
    }

    private const string ArgumentRules = """
        using Makoto;

        namespace Demo;

        public static class Calls
        {
            public static int Count;
        }

        [ValidationRule]
        public sealed class ZeroRule : ValidationRule<string>
        {
            public override bool Evaluate(string value)
            {
                Calls.Count++;
                return value.Length == 0;
            }
        }

        [ValidationRule]
        public sealed class OneRule : ValidationRule<string, int>
        {
            public override bool Evaluate(string value, int length)
            {
                Calls.Count++;
                return value.Length == length;
            }
        }

        [ValidationRule]
        public sealed class TwoRule : ValidationRule<string, int, char>
        {
            public override bool Evaluate(string value, int index, char expected)
            {
                Calls.Count++;
                return value[index] == expected;
            }
        }

        [ValidationRule]
        public sealed class ValueOneRule : ValidationRule<int, int>
        {
            public override bool Evaluate(int value, int limit)
            {
                Calls.Count++;
                return value < limit;
            }
        }

        [ValidationRule]
        public sealed class ValueTwoRule : ValidationRule<int, int, string?>
        {
            public override bool Evaluate(int value, int limit, string? label)
            {
                Calls.Count++;
                return value < limit && label is null;
            }
        }
        """;

    [Theory]
    [InlineData("ZeroRule", "Zero", new string[0])]
    [InlineData("OneRule", "One", new[] { "length" })]
    [InlineData("TwoRule", "Two", new[] { "index", "expected" })]
    public void Argument_names_come_from_the_Evaluate_override(string className, string method, string[] arguments)
    {
        var assembly = RuleAssert.Compiles(ArgumentRules).Load();

        var parameters = RuleAssert.Extension(assembly, $"Demo.{className}Extensions", method).GetParameters();

        Assert.Equal("value", parameters[0].Name);
        Assert.Equal(arguments, parameters.Skip(1).Select(p => p.Name));
    }

    [Fact]
    public void Reference_rules_reject_null_without_calling_Evaluate()
    {
        var assembly = RuleAssert.Compiles(ArgumentRules).Load();
        var calls = assembly.GetType("Demo.Calls")!.GetField("Count")!;

        Assert.Equal(false, RuleAssert.Extension(assembly, "Demo.ZeroRuleExtensions", "Zero").Invoke(null, [null]));
        Assert.Equal(false, RuleAssert.Extension(assembly, "Demo.OneRuleExtensions", "One").Invoke(null, [null, 1]));
        Assert.Equal(false, RuleAssert.Extension(assembly, "Demo.TwoRuleExtensions", "Two").Invoke(null, [null, 0, 'a']));
        Assert.Equal(0, calls.GetValue(null));

        Assert.Equal(true, RuleAssert.Extension(assembly, "Demo.TwoRuleExtensions", "Two").Invoke(null, ["a", 0, 'a']));
        Assert.Equal(1, calls.GetValue(null));
    }

    [Fact]
    public void Value_rules_with_arguments_get_a_nullable_overload()
    {
        var assembly = RuleAssert.Compiles(ArgumentRules).Load();
        var calls = assembly.GetType("Demo.Calls")!.GetField("Count")!;

        var one = assembly.GetType("Demo.ValueOneRuleExtensions")!;
        var plain = one.GetMethod("ValueOne", [typeof(int), typeof(int)])!;
        var nullable = one.GetMethod("ValueOne", [typeof(int?), typeof(int)])!;
        var two = assembly.GetType("Demo.ValueTwoRuleExtensions")!.GetMethod("ValueTwo", [typeof(int?), typeof(int), typeof(string)])!;

        Assert.Equal(false, nullable.Invoke(null, [null, 5]));
        Assert.Equal(false, two.Invoke(null, [null, 5, null]));
        Assert.Equal(0, calls.GetValue(null));

        Assert.Equal(true, plain.Invoke(null, [1, 5]));
        Assert.Equal(true, nullable.Invoke(null, [1, 5]));
        Assert.Equal(true, two.Invoke(null, [1, 5, null]));
        Assert.Equal(3, calls.GetValue(null));
    }

    [Fact]
    public void Nullable_string_rule_receives_the_null()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class IsNullRule : ValidationRule<string?>
            {
                public override bool Evaluate(string? value) => value is null;
            }
            """);

        Assert.Contains("IsNull(this string? value) => s_rule.Evaluate(value);", result.RuleSource("IsNullRule.Rule.g.cs"));

        var method = RuleAssert.Extension(result.Load(), "IsNullRuleExtensions", "IsNull");
        Assert.Equal(true, method.Invoke(null, [null]));
        Assert.Equal(false, method.Invoke(null, ["a"]));
    }

    [Fact]
    public void Nullable_value_rule_receives_the_null_and_gets_one_overload()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class NoValueRule : ValidationRule<int?>
            {
                public override bool Evaluate(int? value) => !value.HasValue;
            }
            """);

        var method = RuleAssert.Extension(result.Load(), "NoValueRuleExtensions", "NoValue");
        Assert.Equal(typeof(int?), method.GetParameters()[0].ParameterType);
        Assert.Equal(true, method.Invoke(null, [null]));
    }

    [Fact]
    public void Open_rule_receives_the_null()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class SeesNullRule<T> : ValidationRule<T>
            {
                public override bool Evaluate(T value) => value is null;
            }
            """);

        var assembly = result.Load();
        Assert.Equal(true, RuleAssert.Extension(assembly, "SeesNullRuleExtensions", "SeesNull", typeof(string)).Invoke(null, [null]));
        Assert.Equal(true, RuleAssert.Extension(assembly, "SeesNullRuleExtensions", "SeesNull", typeof(int?)).Invoke(null, [null]));
        Assert.Equal(false, RuleAssert.Extension(assembly, "SeesNullRuleExtensions", "SeesNull", typeof(int)).Invoke(null, [0]));
    }

    [Fact]
    public void Closed_generic_value_type_is_a_reference_rule()
    {
        var result = RuleAssert.Compiles("""
            using System.Collections.Generic;
            using Makoto;

            [ValidationRule]
            public sealed class HasItemsRule<T> : ValidationRule<List<T>>
            {
                public override bool Evaluate(List<T> value) => value.Count != 0;
            }
            """);

        Assert.Contains("value is not null && Holder<T>.Instance.Evaluate(value)", result.RuleSource("HasItemsRule_1.Rule.g.cs"));
    }

    [Fact]
    public void Rule_over_an_intermediate_abstract_base()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class StringRuleBase : ValidationRule<string>
            {
                protected static bool IsShort(string value) => value.Length < 3;
            }

            [ValidationRule]
            public sealed class ShortRule : StringRuleBase
            {
                public override bool Evaluate(string value) => IsShort(value);
            }
            """);

        var method = RuleAssert.Extension(result.Load(), "ShortRuleExtensions", "Short");
        Assert.Equal(false, method.Invoke(null, [null]));
        Assert.Equal(true, method.Invoke(null, ["ab"]));
    }

    [Fact]
    public void Rule_over_a_generic_base_two_levels_down()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class LimitRule<T> : ValidationRule<T, int>;

            public abstract class StringLimitRule : LimitRule<string>;

            [ValidationRule]
            public sealed class MaxRule : StringLimitRule
            {
                public override bool Evaluate(string value, int limit) => value.Length <= limit;
            }
            """);

        var parameters = RuleAssert.Extension(result.Load(), "MaxRuleExtensions", "Max").GetParameters();
        Assert.Equal([typeof(string), typeof(int)], parameters.Select(p => p.ParameterType));
        Assert.Equal("limit", parameters[1].Name);
    }

    [Fact]
    public void Generic_rule_over_a_generic_base()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class ArrayRule<TElement> : ValidationRule<TElement[]>;

            [ValidationRule]
            public sealed class SingleItemRule<T> : ArrayRule<T>
            {
                public override bool Evaluate(T[] value) => value.Length == 1;
            }

            public static class Caller
            {
                public static bool Call() => new[] { 1 }.SingleItem();
            }
            """);

        Assert.Equal(true, result.Load().GetType("Caller")!.GetMethod("Call")!.Invoke(null, []));
    }

    [Fact]
    public void Evaluate_inherited_from_an_abstract_base()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            public abstract class PositiveRuleBase : ValidationRule<int>
            {
                public override bool Evaluate(int value) => value > 0;
            }

            [ValidationRule]
            public sealed class PositiveRule : PositiveRuleBase;
            """);

        var positive = result.Load().GetType("PositiveRuleExtensions")!.GetMethod("Positive", [typeof(int)])!;
        Assert.Equal(true, positive.Invoke(null, [1]));
    }

    private const string RegexRule = """
        using System.Text.RegularExpressions;
        using Makoto;

        namespace Demo;

        [ValidationRule(Message = "'{PropertyName}' is not a slug.")]
        public partial class SlugRule : ValidationRule<string>
        {
            public override bool Evaluate(string value) => SlugRegex.IsMatch(value);

            [GeneratedRegex("^[a-z0-9-]+$")]
            private static partial Regex SlugRegex { get; }
        }
        """;

    [Fact]
    public void GeneratedRegex_partial_property_rule_compiles_with_the_regex_generator()
    {
        var harness = GeneratorHarness.Run(RegexRule);
        Assert.Empty(harness.GeneratorDiagnostics);

        // The harness only runs Makoto's generators, so the partial property has no implementation until this pass.
        Assert.Contains(harness.CompileErrors, d => d.Id == "CS9248");

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [LoadRegexGenerator()],
            parseOptions: (CSharpParseOptions)harness.Compilation.SyntaxTrees.First().Options);
        driver.RunGeneratorsAndUpdateCompilation(harness.Compilation, out var output, out var diagnostics);

        Assert.Empty(diagnostics);
        var result = harness with { Compilation = (CSharpCompilation)output };
        Assert.Empty(result.CompileErrors);

        var assembly = result.Load();
        Assert.True(assembly.GetType("Demo.SlugRule")!.IsSealed);

        var slug = RuleAssert.Extension(assembly, "Demo.SlugRuleExtensions", "Slug");
        Assert.Equal(true, slug.Invoke(null, ["a-b-1"]));
        Assert.Equal(false, slug.Invoke(null, ["A B"]));
        Assert.Equal(false, slug.Invoke(null, [null]));
    }

    // Ships in the targeting pack, next to the runtime this test runs on.
    private static ISourceGenerator LoadRegexGenerator()
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var packs = Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "packs", "Microsoft.NETCore.App.Ref"));
        var path = Directory.EnumerateFiles(packs, "System.Text.RegularExpressions.Generator.dll", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Last();

        // By name: GetTypes would also load the assembly's code fix, which needs Workspaces.
        var type = Assembly.LoadFrom(path).GetType("System.Text.RegularExpressions.Generator.RegexGenerator", throwOnError: true)!;
        return ((IIncrementalGenerator)Activator.CreateInstance(type)!).AsSourceGenerator();
    }
}
