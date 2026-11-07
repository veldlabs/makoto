using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Makoto.Generators.Tests.Rules;

/// <summary>Which rule a call binds to is the contract of the shared names, and it is decided at compile time.</summary>
public class BuiltInRuleBindingTests
{
    private static GeneratorHarness.Result CompileCall(string receiver, string call) =>
        GeneratorHarness.Run($$"""
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using Makoto.Rules;

            public static class Caller
            {
                public static bool Call({{receiver}} x) => x.{{call}};
            }
            """);

    private static IMethodSymbol Bound(GeneratorHarness.Result result)
    {
        var tree = result.Compilation.SyntaxTrees.First();
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        return (IMethodSymbol)result.Compilation.GetSemanticModel(tree).GetSymbolInfo(call).Symbol!;
    }

    [Theory]
    [InlineData("int", "GreaterThan(1)", "GreaterThanRuleExtensions")]
    [InlineData("int?", "GreaterThan(1)", "GreaterThanNullableRuleExtensions")]
    [InlineData("int", "GreaterThanOrEqual(1)", "GreaterThanOrEqualRuleExtensions")]
    [InlineData("int?", "GreaterThanOrEqual(1)", "GreaterThanOrEqualNullableRuleExtensions")]
    [InlineData("int", "LessThan(1)", "LessThanRuleExtensions")]
    [InlineData("int?", "LessThan(1)", "LessThanNullableRuleExtensions")]
    [InlineData("int", "LessThanOrEqual(1)", "LessThanOrEqualRuleExtensions")]
    [InlineData("int?", "LessThanOrEqual(1)", "LessThanOrEqualNullableRuleExtensions")]
    [InlineData("int", "Between(1, 2)", "BetweenRuleExtensions")]
    [InlineData("int?", "Between(1, 2)", "BetweenNullableRuleExtensions")]
    [InlineData("decimal", "GreaterThan(1)", "GreaterThanRuleExtensions")]
    [InlineData("decimal?", "GreaterThan(1)", "GreaterThanNullableRuleExtensions")]
    [InlineData("decimal?", "Between(0.5m, 2)", "BetweenNullableRuleExtensions")]
    [InlineData("DateTime", "GreaterThan(DateTime.MinValue)", "GreaterThanRuleExtensions")]
    [InlineData("DateTime?", "GreaterThan(DateTime.MinValue)", "GreaterThanNullableRuleExtensions")]
    [InlineData("string", "GreaterThan(\"a\")", "GreaterThanRuleExtensions")]
    [InlineData("string?", "GreaterThan(\"a\")", "GreaterThanRuleExtensions")]
    [InlineData("DayOfWeek", "IsDefined()", "IsDefinedRuleExtensions")]
    [InlineData("DayOfWeek?", "IsDefined()", "IsDefinedRuleExtensions")]
    [InlineData("int?", "NotNull()", "NotNullRuleExtensions")]
    [InlineData("string?", "NotNull()", "NotNullRuleExtensions")]
    [InlineData("string", "NotEmpty()", "NotEmptyRuleExtensions")]
    [InlineData("string?", "NotEmpty()", "NotEmptyRuleExtensions")]
    [InlineData("List<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("List<string>?", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("int[]", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("HashSet<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("Dictionary<int, string>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("ICollection<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("IList<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("IReadOnlyCollection<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("IReadOnlyList<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("IEnumerable<int>", "NotEmpty()", "NotEmptyCollectionRuleExtensions")]
    [InlineData("ICollection<int>", "MinCount(1)", "MinCountRuleExtensions")]
    [InlineData("IReadOnlyCollection<int>", "MaxCount(1)", "MaxCountRuleExtensions")]
    public void Call_binds_to(string receiver, string call, string extensionClass)
    {
        var result = CompileCall(receiver, call);

        Assert.Empty(result.CompileErrors);
        Assert.Equal(extensionClass, Bound(result).ContainingType.Name);
    }

    [Theory]
    [InlineData("string?", "GreaterThan(\"a\")")]
    [InlineData("string?", "Between(\"a\", \"b\")")]
    [InlineData("int?", "NotNull()")]
    public void Nullable_receivers_raise_no_warnings(string receiver, string call)
    {
        var result = CompileCall(receiver, call);

        Assert.Empty(result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Warning));
    }

    // Limits are expected to be constants, so this stays a compile error rather than growing another overload.
    [Theory]
    [InlineData("GreaterThan((int?)1)")]
    [InlineData("Between((int?)1, 2)")]
    public void A_nullable_limit_does_not_compile(string call) => Assert.NotEmpty(CompileCall("int?", call).CompileErrors);

    // Why the collection rules take IEnumerable<T> instead of a pair of overloads.
    [Fact]
    public void Overloads_on_both_collection_interfaces_are_ambiguous()
    {
        var result = GeneratorHarness.Run("""
            using System.Collections.Generic;

            public static class Extensions
            {
                public static bool Has<T>(this ICollection<T> value) => true;
                public static bool Has<T>(this IReadOnlyCollection<T> value) => true;
            }

            public static class Caller
            {
                public static bool Call(List<int> value) => value.Has();
            }
            """);

        Assert.Contains(result.CompileErrors, d => d.Id == "CS0121");
    }

    // Why the built-in rules are StartsWithText and EndsWithText.
    [Fact]
    public void An_instance_method_beats_a_rule_with_the_same_name()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;

            [ValidationRule]
            public sealed class StartsWithRule : ValidationRule<string, string>
            {
                public override bool Evaluate(string value, string prefix) => true;
            }

            public static class Caller
            {
                public static bool Call(string x) => x.StartsWith("a");
            }
            """);

        Assert.Empty(result.CompileErrors);
        Assert.Equal(SpecialType.System_String, Bound(result).ContainingType.SpecialType);
    }

    [Fact]
    public void A_rule_in_the_user_namespace_beats_a_built_in_one()
    {
        var result = RuleAssert.Compiles("""
            namespace Demo
            {
                using Makoto;

                [ValidationRule]
                public sealed class IsEmailRule : ValidationRule<string>
                {
                    public override bool Evaluate(string value) => true;
                }

                public static class Caller
                {
                    public static bool Call(string value) => value.IsEmail();
                }
            }
            """);

        Assert.Equal("Demo", Bound(result).ContainingNamespace.Name);
    }
}
