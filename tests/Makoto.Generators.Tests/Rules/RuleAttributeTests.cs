using System.Reflection;

namespace Makoto.Generators.Tests.Rules;

public class RuleAttributeTests
{
    [Theory]
    [InlineData("IsEvenValidationRule", "IsEven")]
    [InlineData("IsEvenRule", "IsEven")]
    [InlineData("IsEven", "IsEven")]
    [InlineData("Rule", "Rule")]
    [InlineData("ValidationRuleRule", "ValidationRule")]
    public void Method_name_defaults_to_the_class_name_without_its_suffix(string className, string method)
    {
        var result = RuleAssert.Compiles($$"""
            using Makoto;

            [ValidationRule]
            public sealed class {{className}} : ValidationRule<int>
            {
                public override bool Evaluate(int value) => true;
            }
            """);

        var extensions = result.Load().GetType($"{className}Extensions")!;
        Assert.Contains(extensions.GetMethods(), m => m.Name == method);
    }

    [Fact]
    public void Name_renames_the_method_and_the_default_code()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Name = "Even")]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }

            public static class Caller
            {
                public static bool Call(string value) => value.Even();
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "Even").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("Even", rule.Code);
    }

    [Fact]
    public void Code_changes_the_code_and_leaves_the_method_name()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Code = "EVEN_001")]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "IsEven").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("EVEN_001", rule.Code);
    }

    [Fact]
    public void Name_and_code_are_independent()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Name = "Even", Code = "EVEN_001")]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "Even").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("EVEN_001", rule.Code);
    }

    [Fact]
    public void Message_defaults_to_the_generic_message()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "IsEven").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("'{PropertyName}' is not valid.", rule.Message);
    }

    [Fact]
    public void Message_is_kept_as_a_template()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Message = "'{PropertyName}' must be under {max}.")]
            public sealed class UnderRule : ValidationRule<int, int>
            {
                public override bool Evaluate(int value, int max) => value < max;
            }
            """);

        var attributes = result.Load().GetType("UnderRuleExtensions")!.GetMethods()
            .Where(m => m.Name == "Under")
            .Select(m => m.GetCustomAttribute<GeneratedRuleAttribute>()!.Message);
        Assert.Equal(["'{PropertyName}' must be under {max}.", "'{PropertyName}' must be under {max}."], attributes);
    }

    [Fact]
    public void Message_from_a_constant()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            internal static class Messages
            {
                public const string Even = "must be even";
            }

            [ValidationRule(Message = "'{PropertyName}' " + Messages.Even)]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "IsEven").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("'{PropertyName}' must be even", rule.Message);
    }

    [Fact]
    public void Message_with_characters_that_need_escaping()
    {
        var result = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Message = "say \"hi\"\\\n\t{0} é 😀")]
            public sealed class IsEvenRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => true;
            }
            """);

        var rule = RuleAssert.Extension(result.Load(), "IsEvenRuleExtensions", "IsEven").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal("say \"hi\"\\\n\t{0} é 😀", rule.Message);
    }

    [Fact]
    public void GeneratedRule_carries_the_rule_type_code_and_message()
    {
        var assembly = RuleAssert.Compiles("""
            using Makoto;

            namespace Demo;

            [ValidationRule(Code = "NOPE", Message = "No.")]
            public sealed class NopeRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => false;
            }
            """).Load();

        var rule = RuleAssert.Extension(assembly, "Demo.NopeRuleExtensions", "Nope").GetCustomAttribute<GeneratedRuleAttribute>()!;
        Assert.Equal(assembly.GetType("Demo.NopeRule"), rule.Rule);
        Assert.Equal("NOPE", rule.Code);
        Assert.Equal("No.", rule.Message);
    }

    [Fact]
    public void GeneratedRule_is_on_both_overloads_of_a_value_rule()
    {
        var assembly = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule(Code = "NOPE", Message = "No.")]
            public sealed class NopeRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => false;
            }
            """).Load();

        var attributes = assembly.GetType("NopeRuleExtensions")!.GetMethods()
            .Where(m => m.Name == "Nope")
            .Select(m => m.GetCustomAttribute<GeneratedRuleAttribute>()!)
            .ToArray();

        Assert.Equal(2, attributes.Length);
        Assert.All(attributes, a => Assert.Equal(("NOPE", "No."), (a.Code, a.Message)));
    }

    [Theory]
    [InlineData("OneRule<T>", "T", "OneRule`1", "ValidationRule<T>", "T value")]
    [InlineData("TwoRule<T, U>", "T, U", "TwoRule`2", "ValidationRule<T, U>", "T value, U other")]
    public void GeneratedRule_names_the_open_generic_rule(string declaration, string typeParameters, string metadataName, string baseType, string parameters)
    {
        var assembly = RuleAssert.Compiles($$"""
            using Makoto;

            namespace Demo;

            [ValidationRule]
            public sealed class {{declaration}} : {{baseType}}
            {
                public override bool Evaluate({{parameters}}) => true;
            }
            """).Load();

        var open = assembly.GetType($"Demo.{metadataName}")!;
        var extensionClass = $"Demo.{metadataName[..metadataName.IndexOf('`')]}Extensions";
        var rule = assembly.GetType(extensionClass)!.GetMethods()
            .Single(m => m.Name is "One" or "Two")
            .GetCustomAttribute<GeneratedRuleAttribute>()!;

        Assert.True(open.IsGenericTypeDefinition);
        Assert.Equal(typeParameters.Split(", ").Length, open.GetGenericArguments().Length);
        Assert.Equal(open, rule.Rule);
    }

    [Fact]
    public void Extension_is_aggressively_inlined()
    {
        var assembly = RuleAssert.Compiles("""
            using Makoto;

            [ValidationRule]
            public sealed class NopeRule : ValidationRule<string>
            {
                public override bool Evaluate(string value) => false;
            }
            """).Load();

        var flags = RuleAssert.Extension(assembly, "NopeRuleExtensions", "Nope").GetMethodImplementationFlags();
        Assert.True(flags.HasFlag(MethodImplAttributes.AggressiveInlining));
    }
}
