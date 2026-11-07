using Makoto.Generators.Model;

namespace Makoto.Generators.Rules;

/// <summary>How a rule's extension method treats a null receiver.</summary>
internal enum RuleValueKind
{
    /// <summary><c>ValidationRule&lt;string&gt;</c>: takes <c>string?</c> and null fails without calling the rule.</summary>
    NonNullableReference,

    /// <summary><c>ValidationRule&lt;string?&gt;</c>, or an unconstrained <c>T</c>: passed through, the rule sees the null.</summary>
    PassThrough,

    /// <summary><c>ValidationRule&lt;int&gt;</c>, or a <c>T : struct</c>: passed through, plus a nullable overload where null fails.</summary>
    ValueType,
}

internal sealed record RuleArgument(string Type, string Name);

/// <summary>Everything needed to emit a rule's extension and to match calls to it from validators.</summary>
internal sealed record RuleInfo(
    string? Namespace,
    string ClassName,
    string QualifiedName,
    string Accessibility,
    bool EmitSealed,
    EquatableArray<string> TypeParameters,
    EquatableArray<string> Constraints,
    string MethodName,
    string Code,
    string Message,
    string ValueType,
    RuleValueKind ValueKind,
    EquatableArray<RuleArgument> Arguments);

/// <summary>The result of inspecting one [ValidationRule] class. Rule is null when it can't be emitted.</summary>
internal sealed record RuleDiscoveryResult(RuleInfo? Rule, EquatableArray<DiagnosticInfo> Diagnostics);
