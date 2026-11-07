using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Makoto.Generators.Model;

internal static class Diagnostics
{
    private const string Category = "Makoto";

    public static readonly DiagnosticDescriptor RuleMissingBase = Error(
        "MKO001", "Rule has no ValidationRule base", "'{0}' is marked [ValidationRule] but does not derive from ValidationRule<...>");

    public static readonly DiagnosticDescriptor RuleNotSealed = Error(
        "MKO002", "Rule must be sealed or partial", "Rule '{0}' must be sealed or partial, or its Evaluate call cannot be devirtualized");

    public static readonly DiagnosticDescriptor RuleHasState = Error(
        "MKO003", "Rule has mutable state", "Rule '{0}' has mutable instance field '{1}'. Rules are shared singletons and only evaluate their input.");

    public static readonly DiagnosticDescriptor RuleNoDefaultConstructor = Error(
        "MKO004", "Rule needs a parameterless constructor", "Rule '{0}' needs an accessible parameterless constructor");

    public static readonly DiagnosticDescriptor RuleNotTopLevel = Error(
        "MKO005", "Rule must be top-level", "Rule '{0}' must not be nested in another type");

    public static readonly DiagnosticDescriptor RuleInvalidName = Error(
        "MKO006", "Invalid rule name", "Rule '{0}' has the name '{1}', which is not a valid method name");

    public static readonly DiagnosticDescriptor ValidatorNotPartial = Error(
        "MKO010", "Validator must be partial", "Validator '{0}' must be partial");

    public static readonly DiagnosticDescriptor ValidatorShape = Error(
        "MKO011", "Unsupported validator shape", "Validator '{0}' must be a top-level, non-generic class deriving directly from Validator<T>");

    public static readonly DiagnosticDescriptor UnsupportedStatement = Error(
        "MKO012", "Unsupported statement in Configure", "Configure only supports Property(...) and Each(...) chains. It never runs, so this statement would have no effect.");

    public static readonly DiagnosticDescriptor LambdaCaptures = Error(
        "MKO013", "Lambda captures state", "This lambda is compiled into a static method and cannot capture '{0}'");

    public static readonly DiagnosticDescriptor ExpectedLambda = Error(
        "MKO014", "Expected a lambda", "'{0}' takes an inline lambda here");

    public static readonly DiagnosticDescriptor InvalidSelector = Error(
        "MKO015", "Invalid selector", "Selectors must be a member access on the lambda parameter, e.g. x => x.Address.City");

    public static readonly DiagnosticDescriptor NonConstantArgument = Error(
        "MKO016", "Argument must be constant", "The argument of '{0}' must be a compile-time constant");

    public static readonly DiagnosticDescriptor ValidateWithMismatch = Error(
        "MKO017", "ValidateWith type mismatch", "'{0}' does not validate '{1}'");

    public static readonly DiagnosticDescriptor ModifierWithoutRequire = Error(
        "MKO018", "Modifier has no Require", "'{0}' must follow a Require");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}

/// <summary>A diagnostic in a form that can live in incremental pipeline models.</summary>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    string? FilePath,
    TextSpan Span,
    LinePositionSpan LineSpan,
    EquatableArray<string> Arguments)
{
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string[] arguments)
    {
        var lineSpan = location?.GetLineSpan();
        return new DiagnosticInfo(
            descriptor,
            lineSpan?.Path,
            location?.SourceSpan ?? default,
            lineSpan?.Span ?? default,
            arguments.ToEquatableArray());
    }

    public Diagnostic ToDiagnostic()
    {
        var location = FilePath is null ? Location.None : Location.Create(FilePath, Span, LineSpan);
        return Diagnostic.Create(Descriptor, location, Arguments.Cast<object>().ToArray());
    }
}
