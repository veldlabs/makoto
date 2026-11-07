using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Makoto.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Makoto.Generators.Rules;

/// <summary>Finds [ValidationRule] classes. Shared by the rule and validator generators.</summary>
internal static class RuleDiscovery
{
    public const string DefaultMessage = "'{PropertyName}' is not valid.";

    private const string AttributeName = "Makoto.ValidationRuleAttribute";

    public static IncrementalValuesProvider<RuleDiscoveryResult> Create(IncrementalGeneratorInitializationContext context) =>
        context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is ClassDeclarationSyntax,
            static (syntax, cancellationToken) => Inspect(syntax, cancellationToken));

    private static RuleDiscoveryResult Inspect(GeneratorAttributeSyntaxContext syntax, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var type = (INamedTypeSymbol)syntax.TargetSymbol;
        var declaration = (ClassDeclarationSyntax)syntax.TargetNode;
        var location = declaration.Identifier.GetLocation();
        var diagnostics = new List<DiagnosticInfo>();

        var ruleBase = FindRuleBase(type);
        if (ruleBase is null)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleMissingBase, location, type.Name));
            return new RuleDiscoveryResult(null, diagnostics.ToEquatableArray());
        }

        var isPartial = declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        if (!type.IsSealed && !isPartial)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleNotSealed, location, type.Name));
        }

        if (type.ContainingType is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleNotTopLevel, location, type.Name));
        }

        if (!type.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility >= Accessibility.Internal))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleNoDefaultConstructor, location, type.Name));
        }

        foreach (var field in OwnTypes(type, ruleBase).SelectMany(t => t.GetMembers().OfType<IFieldSymbol>()))
        {
            if (!field.IsStatic && !field.IsReadOnly && !field.IsConst)
            {
                var fieldLocation = field.Locations.FirstOrDefault(l => l.IsInSource) ?? location;
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleHasState, fieldLocation, type.Name, field.Name));
            }
        }

        var attribute = syntax.Attributes[0];
        var methodName = NamedString(attribute, "Name") ?? StripSuffix(type.Name);
        // IsValidIdentifier accepts reserved keywords; contextual keywords such as 'value' are fine as method names.
        if (!SyntaxFacts.IsValidIdentifier(methodName) || SyntaxFacts.GetKeywordKind(methodName) != SyntaxKind.None)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.RuleInvalidName, location, type.Name, methodName));
        }

        if (diagnostics.Count != 0)
        {
            return new RuleDiscoveryResult(null, diagnostics.ToEquatableArray());
        }

        var evaluate = ruleBase.GetMembers("Evaluate").OfType<IMethodSymbol>().Single();
        var overridden = OwnTypes(type, ruleBase)
            .SelectMany(t => t.GetMembers("Evaluate").OfType<IMethodSymbol>())
            .FirstOrDefault(m => Overrides(m, evaluate));
        var value = ruleBase.TypeArguments[0];

        var rule = new RuleInfo(
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            type.Name,
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            !type.IsSealed,
            type.TypeParameters.Select(p => p.Name).ToEquatableArray(),
            type.TypeParameters.Select(p => p.ToConstraintClause()).OfType<string>().ToEquatableArray(),
            methodName,
            NamedString(attribute, "Code") ?? methodName,
            NamedString(attribute, "Message") ?? DefaultMessage,
            value.ToQualifiedString(),
            GetValueKind(value),
            (overridden ?? evaluate).Parameters.Skip(1)
                .Select((p, i) => new RuleArgument(evaluate.Parameters[i + 1].Type.ToQualifiedString(), p.Name))
                .ToEquatableArray());

        return new RuleDiscoveryResult(rule, EquatableArray<DiagnosticInfo>.Empty);
    }

    private static INamedTypeSymbol? FindRuleBase(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current is { Name: "ValidationRule", ContainingNamespace: { Name: "Makoto", ContainingNamespace.IsGlobalNamespace: true } })
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Whether <paramref name="method"/> overrides <paramref name="target"/>, directly or through intermediate overrides. Other overloads named Evaluate don't count.</summary>
    private static bool Overrides(IMethodSymbol method, IMethodSymbol target)
    {
        for (var current = method.OverriddenMethod; current is not null; current = current.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The rule and its base classes, up to but excluding the ValidationRule base.</summary>
    private static IEnumerable<INamedTypeSymbol> OwnTypes(INamedTypeSymbol type, INamedTypeSymbol ruleBase)
    {
        for (var current = type; current is not null && !SymbolEqualityComparer.Default.Equals(current, ruleBase); current = current.BaseType)
        {
            yield return current;
        }
    }

    private static RuleValueKind GetValueKind(ITypeSymbol value)
    {
        if (value.NullableAnnotation == NullableAnnotation.Annotated)
        {
            return RuleValueKind.PassThrough;
        }

        if (value.IsValueType)
        {
            return RuleValueKind.ValueType;
        }

        return value.TypeKind == TypeKind.TypeParameter ? RuleValueKind.PassThrough : RuleValueKind.NonNullableReference;
    }

    private static string? NamedString(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(a => a.Key == name).Value.Value as string;

    private static string StripSuffix(string name)
    {
        foreach (var suffix in new[] { "ValidationRule", "Rule" })
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return name.Substring(0, name.Length - suffix.Length);
            }
        }

        return name;
    }
}
