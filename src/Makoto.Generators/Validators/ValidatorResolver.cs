using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Makoto.Generators.Model;
using Makoto.Generators.Rules;

namespace Makoto.Generators.Validators;

/// <summary>Turns a parsed validator into one that's ready to emit, settling every code and message.</summary>
internal static class ValidatorResolver
{
    private const string DefaultCode = "Require";

    // One pass, so a name or argument that itself looks like a placeholder is never substituted again.
    private static readonly Regex Placeholder = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant);

    public static ValidatorResult Resolve(ValidatorResult result, EquatableArray<RuleInfo> rules)
    {
        if (result.Model is not { } validator)
        {
            return result;
        }

        var chains = validator.Chains
            .Select(chain => chain with
            {
                Steps = chain.Steps.Select(step => step is RequireStep require ? Resolve(require, chain, validator, rules) : step).ToEquatableArray(),
            })
            .ToEquatableArray();

        return result with { Model = validator with { Chains = chains, ImportedNamespaces = EquatableArray<string>.Empty } };
    }

    private static ChainStep Resolve(RequireStep step, RuleChain chain, ValidatorModel validator, EquatableArray<RuleInfo> rules)
    {
        var call = step.Rule;
        var rule = call is { IsBound: false } ? FindRule(call, chain, validator, rules) : null;
        var parameters = rule?.Arguments.Select(a => a.Name).ToArray() ?? call?.Parameters.ToArray() ?? [];

        var values = new Dictionary<string, string> { ["PropertyName"] = chain.Name };
        var arguments = call?.Arguments.ToArray() ?? [];
        for (var i = 0; i < arguments.Length; i++)
        {
            var name = arguments[i].Name ?? (i < parameters.Length ? parameters[i] : null);
            if (name is not null && arguments[i].Constant is { } constant)
            {
                values[name] = constant;
            }
        }

        var message = Placeholder.Replace(
            step.Message ?? rule?.Message ?? call?.Message ?? RuleDiscovery.DefaultMessage,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

        return step with { Code = step.Code ?? rule?.Code ?? call?.Code ?? DefaultCode, Message = message, Rule = null };
    }

    /// <summary>
    /// Extension methods with the same name can live side by side, so the namespace decides first, then the shape of
    /// the call. The rule table isn't in a stable order, the result has to be.
    /// </summary>
    private static RuleInfo? FindRule(RuleCall call, RuleChain chain, ValidatorModel validator, EquatableArray<RuleInfo> rules)
    {
        var valueType = chain.ValueType.TrimEnd('?');
        return rules
            .Where(r => r.MethodName == call.Name)
            .OrderByDescending(r => r.Namespace is null || validator.ImportedNamespaces.Contains(r.Namespace))
            .ThenByDescending(r => r.Arguments.Length == call.Arguments.Length)
            .ThenByDescending(r => r.ValueType.TrimEnd('?') == valueType)
            .ThenBy(r => r.QualifiedName, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
