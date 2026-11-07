using System.Reflection;
using System.Text.RegularExpressions;

namespace Makoto.Tests.Rules;

using Makoto.Rules;

public partial class RuleContractTests
{
    private static readonly MethodInfo[] Extensions = typeof(NotEmptyRule).Assembly.GetTypes()
        .Where(t => t.Namespace == "Makoto.Rules" && t.Name.EndsWith("RuleExtensions", StringComparison.Ordinal))
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
        .ToArray();

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder { get; }

    [Fact]
    public void Every_rule_has_its_extension()
    {
        string[] expected =
        [
            "Between", "ContainsText", "EndsWithText", "GreaterThan", "GreaterThanOrEqual", "IsAscii", "IsDefined",
            "IsDigits", "IsEmail", "IsGuid", "IsUrl", "LengthBetween", "LessThan", "LessThanOrEqual", "MaxCount",
            "MaxLength", "MinCount", "MinLength", "NotBlank", "NotEmpty", "NotNull", "StartsWithText",
        ];

        Assert.Equal(expected, Extensions.Select(m => m.Name).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_rule_is_sealed_and_named_with_a_rule_suffix()
    {
        var rules = typeof(NotEmptyRule).Assembly.GetTypes().Where(t => t.GetCustomAttribute<ValidationRuleAttribute>() is not null);

        Assert.All(rules, rule =>
        {
            Assert.True(rule.IsSealed, rule.Name);
            Assert.True(rule.IsPublic, rule.Name);
            Assert.Equal("Makoto.Rules", rule.Namespace);
            Assert.Matches("Rule(`[0-9])?$", rule.Name);
        });
    }

    [Fact]
    public void Messages_only_use_placeholders_the_method_can_fill()
    {
        Assert.All(Extensions, method =>
        {
            var rule = method.GetCustomAttribute<GeneratedRuleAttribute>();
            Assert.NotNull(rule);
            Assert.Equal(method.Name, rule.Code);
            Assert.StartsWith("'{PropertyName}' ", rule.Message, StringComparison.Ordinal);

            var parameters = method.GetParameters().Skip(1).Select(p => p.Name).ToHashSet();
            var placeholders = Placeholder.Matches(rule.Message).Select(m => m.Groups[1].Value).Where(p => p != "PropertyName");
            Assert.Equal(parameters.Order(), placeholders.Order());
        });
    }

    // A validator resolves its default code and message by method name, so rules sharing a name have to agree.
    [Fact]
    public void Rules_sharing_a_name_share_their_code_and_message()
    {
        Assert.All(Extensions.GroupBy(m => m.Name), group =>
        {
            var defaults = group.Select(m => m.GetCustomAttribute<GeneratedRuleAttribute>()!).Select(a => (a.Code, a.Message)).Distinct();
            Assert.Single(defaults);
        });
    }

    [Fact]
    public void Comparison_twins_share_argument_names()
    {
        Assert.All(Extensions.Where(m => m.GetParameters()[0].ParameterType.Name == "Nullable`1").GroupBy(m => m.Name), group =>
        {
            var open = Extensions.Single(m => m.Name == group.Key && m.GetParameters()[0].ParameterType.Name != "Nullable`1");
            Assert.Equal(open.GetParameters().Select(p => p.Name), group.Single().GetParameters().Select(p => p.Name));
        });
    }
}
