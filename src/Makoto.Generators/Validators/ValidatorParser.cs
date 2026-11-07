using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Makoto.Generators.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Makoto.Generators.Validators;

/// <summary>
/// Reads a [Validator] class and its Configure method. Same-project rule calls don't bind here, so chains are
/// recognized from syntax alone and the semantic model is only asked about types and constants.
/// </summary>
internal static class ValidatorParser
{
    public static ValidatorResult Parse(GeneratorAttributeSyntaxContext syntax, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var type = (INamedTypeSymbol)syntax.TargetSymbol;
        var declaration = (TypeDeclarationSyntax)syntax.TargetNode;

        // [Validator] on two parts of a class is CS0579, and must not turn into two files with one name.
        if (!IsFirstAttributedPart(syntax, type))
        {
            return ValidatorResult.Unusable;
        }

        var location = declaration.Identifier.GetLocation();
        var diagnostics = new List<DiagnosticInfo>();

        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.ValidatorNotPartial, location, type.Name));
        }

        var isValidatorBase = type.BaseType is { } baseType && IsValidatorBase(baseType);
        if (declaration is not ClassDeclarationSyntax || !isValidatorBase || type.ContainingType is not null ||
            type.IsGenericType || type.IsAbstract || type.IsFileLocal)
        {
            diagnostics.Add(DiagnosticInfo.Create(Diagnostics.ValidatorShape, location, type.Name));
        }

        if (diagnostics.Count != 0)
        {
            return new ValidatorResult(null, diagnostics.ToEquatableArray());
        }

        var modelType = type.BaseType!.TypeArguments[0];
        if (modelType.TypeKind == TypeKind.Error)
        {
            return ValidatorResult.Unusable;
        }

        // A partial validator can have Configure in another file, and its usings are the ones that matter.
        var configure = type.GetMembers("Configure").OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.IsOverride && m.Parameters.Length == 0)
            ?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) as MethodDeclarationSyntax;

        var anchor = configure ?? (SyntaxNode)declaration;
        var semantics = anchor.SyntaxTree == syntax.SemanticModel.SyntaxTree
            ? syntax.SemanticModel
            : syntax.SemanticModel.Compilation.GetSemanticModel(anchor.SyntaxTree);

        var reader = new ChainReader(semantics, type, diagnostics, cancellationToken);
        if (configure?.ExpressionBody is { } expressionBody)
        {
            reader.ReadChain(expressionBody.Expression);
        }

        foreach (var statement in configure?.Body?.Statements ?? default)
        {
            if (statement is ExpressionStatementSyntax expression)
            {
                reader.ReadChain(expression.Expression);
            }
            else
            {
                diagnostics.Add(DiagnosticInfo.Create(Diagnostics.UnsupportedStatement, statement.GetLocation()));
            }
        }

        if (diagnostics.Count != 0)
        {
            return new ValidatorResult(null, diagnostics.ToEquatableArray());
        }

        if (!reader.IsUsable)
        {
            return ValidatorResult.Unusable;
        }

        var model = new ValidatorModel(
            declaration.Identifier.Text,
            type.ToQualifiedString(),
            modelType.ToQualifiedString(),
            modelType.IsReferenceType,
            ReadScopes(anchor),
            ReadImportedNamespaces(type, semantics, anchor.SpanStart, cancellationToken),
            reader.Chains.ToEquatableArray());

        return new ValidatorResult(model, EquatableArray<DiagnosticInfo>.Empty);
    }

    private static bool IsFirstAttributedPart(GeneratorAttributeSyntaxContext syntax, INamedTypeSymbol type)
    {
        var attributeClass = syntax.Attributes[0].AttributeClass;
        var first = type.GetAttributes()
            .First(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeClass))
            .ApplicationSyntaxReference;

        return first is not null &&
            first.SyntaxTree == syntax.TargetNode.SyntaxTree &&
            syntax.TargetNode.Span.Contains(first.Span);
    }

    private static bool IsValidatorBase(INamedTypeSymbol type) =>
        type is { Name: "Validator", Arity: 1, ContainingNamespace: { Name: "Makoto", ContainingNamespace.IsGlobalNamespace: true } };

    private static EquatableArray<NamespaceScope> ReadScopes(SyntaxNode anchor)
    {
        var scopes = new List<NamespaceScope>();
        foreach (var node in anchor.Ancestors().Reverse())
        {
            switch (node)
            {
                case CompilationUnitSyntax unit:
                    scopes.Add(new NamespaceScope(null, ReadDirectives(unit.Externs, unit.Usings)));
                    break;
                case BaseNamespaceDeclarationSyntax scope:
                    scopes.Add(new NamespaceScope(scope.Name.ToString(), ReadDirectives(scope.Externs, scope.Usings)));
                    break;
            }
        }

        return scopes.ToEquatableArray();
    }

    // Global usings already apply to the generated file.
    private static EquatableArray<string> ReadDirectives(
        SyntaxList<ExternAliasDirectiveSyntax> externs,
        SyntaxList<UsingDirectiveSyntax> usings) =>
        externs.Select(e => e.ToString())
            .Concat(usings.Where(u => u.GlobalKeyword.IsKind(SyntaxKind.None)).Select(u => u.ToString()))
            .ToEquatableArray();

    /// <summary>The namespaces whose extension methods Configure can see, for picking between same-named rules.</summary>
    private static EquatableArray<string> ReadImportedNamespaces(
        INamedTypeSymbol type,
        SemanticModel semantics,
        int position,
        CancellationToken cancellationToken)
    {
        var namespaces = new List<string>();
        for (var current = type.ContainingNamespace; current is { IsGlobalNamespace: false }; current = current.ContainingNamespace)
        {
            namespaces.Add(current.ToDisplayString());
        }

        foreach (var scope in semantics.GetImportScopes(position, cancellationToken))
        {
            namespaces.AddRange(scope.Imports.Select(i => i.NamespaceOrType).OfType<INamespaceSymbol>().Select(n => n.ToDisplayString()));
        }

        return namespaces.Distinct().OrderBy(n => n, System.StringComparer.Ordinal).ToEquatableArray();
    }

    private sealed class ChainReader(
        SemanticModel semantics,
        INamedTypeSymbol validator,
        List<DiagnosticInfo> diagnostics,
        CancellationToken cancellationToken)
    {
        private int _requires;
        private int _conditions;

        public List<RuleChain> Chains { get; } = [];

        /// <summary>False when the compiler has its own error to report, like an unknown member in a selector.</summary>
        public bool IsUsable { get; private set; } = true;

        public void ReadChain(ExpressionSyntax expression)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var calls = new Stack<(InvocationExpressionSyntax Call, SimpleNameSyntax Name)>();
            var current = expression;
            while (current is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation &&
                !(access.Expression is ThisExpressionSyntax && IsRoot(access.Name)))
            {
                calls.Push((invocation, access.Name));
                current = access.Expression;
            }

            var root = current as InvocationExpressionSyntax;
            var rootName = root?.Expression switch
            {
                SimpleNameSyntax simple => simple,
                MemberAccessExpressionSyntax qualified => qualified.Name,
                _ => null,
            };

            // The root's own arguments never hold rule calls, so unlike the rest of the chain it does bind. Only a
            // method of the validator that hides Property or Each can make this something else.
            if (root is null || rootName is null || !IsRoot(rootName) ||
                semantics.GetSymbolInfo(root, cancellationToken).Symbol is IMethodSymbol { ContainingType: var owner } && !IsValidatorBase(owner))
            {
                Report(Diagnostics.UnsupportedStatement, expression);
                return;
            }

            var isEach = rootName.Identifier.ValueText == "Each";
            var selector = ReadSelector(root, rootName, out var path);
            var valueType = ReadValueType(root);

            var name = path;
            var stopOnFailure = false;
            var steps = new List<ChainStep>();

            foreach (var (call, callName) in calls)
            {
                var method = callName.Identifier.ValueText;
                switch (method)
                {
                    case "Require":
                        ReadRequire(call, callName, steps);
                        break;
                    case "When":
                        ReadWhen(call, callName, steps);
                        break;
                    case "WithMessage":
                        ReadOverride(call, callName, steps, static (step, value) => step with { Message = value });
                        break;
                    case "WithCode":
                        ReadOverride(call, callName, steps, static (step, value) => step with { Code = value });
                        break;
                    case "Named":
                        name = ReadConstant(call, method) ?? name;
                        break;
                    case "StopOnFailure":
                        stopOnFailure = true;
                        break;
                    case "ValidateWith" when callName is GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic:
                        ReadValidateWith(generic.TypeArgumentList.Arguments[0], valueType, steps);
                        break;
                    default:
                        Report(Diagnostics.UnsupportedStatement, call);
                        break;
                }
            }

            // Nothing to do, and the selector doesn't need to be emitted.
            if (selector is null || valueType is null || steps.Count == 0)
            {
                return;
            }

            var selected = valueType;
            var collection = CollectionKind.None;
            if (isEach)
            {
                var body = (ExpressionSyntax)GetLambda(root)!.Body;
                selected = semantics.GetTypeInfo(body, cancellationToken).Type;
                if (selected is null or { TypeKind: TypeKind.Error })
                {
                    IsUsable = false;
                    return;
                }

                collection = ClassifyCollection(selected);
            }

            Chains.Add(new RuleChain(
                name,
                valueType.ToQualifiedString(),
                GetValueKind(valueType),
                selector,
                selected.ToQualifiedString(),
                collection,
                GetValueKind(selected),
                stopOnFailure,
                steps.ToEquatableArray()));
        }

        private static bool IsRoot(SimpleNameSyntax name) => name.Identifier.ValueText is "Property" or "Each";

        private Lambda? ReadSelector(InvocationExpressionSyntax root, SimpleNameSyntax rootName, out string path)
        {
            path = "";
            var lambda = GetLambda(root);
            if (lambda is null || GetParameters(lambda).Count != 1)
            {
                Report(Diagnostics.ExpectedLambda, FirstArgument(root), rootName.Identifier.ValueText);
                return null;
            }

            var members = new List<string>();
            var parameter = GetParameterName(GetParameters(lambda)[0]);
            if (lambda.Body is not ExpressionSyntax body || !TryReadPath(body, parameter, members) || members.Count == 0)
            {
                Report(Diagnostics.InvalidSelector, lambda);
                return null;
            }

            path = string.Join(".", members);
            return ReadLambda(lambda, $"Select{Chains.Count}");
        }

        /// <summary>Walks x.A!.B?.C. A conditional access ends in a member binding, which has no receiver of its own.</summary>
        private static bool TryReadPath(ExpressionSyntax expression, string parameter, List<string> members)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    return TryReadPath(parenthesized.Expression, parameter, members);
                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    return TryReadPath(postfix.Operand, parameter, members);
                case ConditionalAccessExpressionSyntax conditional:
                    return TryReadPath(conditional.Expression, parameter, members) &&
                        TryReadPath(conditional.WhenNotNull, parameter, members);
                case MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    return TryReadPath(access.Expression, parameter, members) && TryAddMember(access.Name, members);
                case MemberBindingExpressionSyntax binding:
                    return TryAddMember(binding.Name, members);
                case IdentifierNameSyntax identifier:
                    return identifier.Identifier.ValueText == parameter;
                default:
                    return false;
            }
        }

        private static bool TryAddMember(SimpleNameSyntax name, List<string> members)
        {
            if (name is not IdentifierNameSyntax || name.IsMissing)
            {
                return false;
            }

            members.Add(name.Identifier.ValueText);
            return true;
        }

        private ITypeSymbol? ReadValueType(InvocationExpressionSyntax root)
        {
            var type = (semantics.GetSymbolInfo(root, cancellationToken).Symbol as IMethodSymbol)?.TypeArguments.FirstOrDefault();
            if (type is null or { TypeKind: TypeKind.Error })
            {
                IsUsable = false;
                return null;
            }

            return type;
        }

        private void ReadRequire(InvocationExpressionSyntax call, SimpleNameSyntax name, List<ChainStep> steps)
        {
            var lambda = ReadPredicate(call, name, $"Require{_requires}");
            if (lambda is null)
            {
                return;
            }

            var syntax = GetLambda(call)!;
            var valueParameter = GetParameterName(GetParameters(syntax).Last());
            steps.Add(new RequireStep(
                _requires++,
                lambda,
                EquatableArray<Lambda>.Empty,
                null,
                null,
                ReadRuleCall(syntax, valueParameter)));
        }

        private void ReadWhen(InvocationExpressionSyntax call, SimpleNameSyntax name, List<ChainStep> steps)
        {
            var lambda = ReadPredicate(call, name, $"When{_conditions}");
            if (lambda is null)
            {
                return;
            }

            _conditions++;
            if (TryGetRequire(name, steps, out var step))
            {
                steps[steps.Count - 1] = step with { Conditions = step.Conditions.Append(lambda).ToEquatableArray() };
            }
        }

        private void ReadOverride(
            InvocationExpressionSyntax call,
            SimpleNameSyntax name,
            List<ChainStep> steps,
            System.Func<RequireStep, string, RequireStep> apply)
        {
            var value = ReadConstant(call, name.Identifier.ValueText);
            if (TryGetRequire(name, steps, out var step) && value is not null)
            {
                steps[steps.Count - 1] = apply(step, value);
            }
        }

        // Named and StopOnFailure belong to the chain, so only a ValidateWith separates a modifier from its Require.
        private bool TryGetRequire(SimpleNameSyntax name, List<ChainStep> steps, out RequireStep step)
        {
            step = (steps.LastOrDefault() as RequireStep)!;
            if (step is null)
            {
                Report(Diagnostics.ModifierWithoutRequire, name, name.Identifier.ValueText);
                return false;
            }

            return true;
        }

        private void ReadValidateWith(TypeSyntax argument, ITypeSymbol? valueType, List<ChainStep> steps)
        {
            var target = semantics.GetTypeInfo(argument, cancellationToken).Type;
            if (target is null or { TypeKind: TypeKind.Error })
            {
                IsUsable = false;
                return;
            }

            if (valueType is not null && !Validates(target, valueType))
            {
                Report(Diagnostics.ValidateWithMismatch, argument, target.ToDisplayString(), valueType.ToDisplayString());
            }

            steps.Add(new ValidateWithStep(target.ToQualifiedString()));
        }

        private static bool Validates(ITypeSymbol target, ITypeSymbol valueType)
        {
            for (var current = target.BaseType; current is not null; current = current.BaseType)
            {
                if (IsValidatorBase(current))
                {
                    return SymbolEqualityComparer.Default.Equals(Unwrap(current.TypeArguments[0]), Unwrap(valueType));
                }
            }

            return false;
        }

        private static ITypeSymbol Unwrap(ITypeSymbol type) =>
            type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
                ? nullable.TypeArguments[0]
                : type;

        private Lambda? ReadPredicate(InvocationExpressionSyntax call, SimpleNameSyntax name, string methodName)
        {
            var lambda = GetLambda(call);
            if (lambda is null || GetParameters(lambda).Count is not (1 or 2))
            {
                Report(Diagnostics.ExpectedLambda, FirstArgument(call), name.Identifier.ValueText);
                return null;
            }

            ReportCaptures(lambda);
            return ReadLambda(lambda, methodName);
        }

        private static Lambda ReadLambda(LambdaExpressionSyntax lambda, string methodName)
        {
            var parameters = GetParameters(lambda);

            // Lambdas can have several discards, methods can't.
            var names = parameters.Count > 1 && parameters.All(p => p.Identifier.ValueText == "_")
                ? parameters.Select((_, i) => $"__discard{i}")
                : parameters.Select(p => p.Identifier.Text);

            return new Lambda(methodName, names.ToEquatableArray(), lambda.Body.ToString(), lambda.Body is BlockSyntax);
        }

        private static LambdaExpressionSyntax? GetLambda(InvocationExpressionSyntax call) =>
            call.ArgumentList.Arguments.FirstOrDefault()?.Expression as LambdaExpressionSyntax;

        private static SeparatedSyntaxList<ParameterSyntax> GetParameters(LambdaExpressionSyntax lambda) => lambda switch
        {
            SimpleLambdaExpressionSyntax simple => SyntaxFactory.SingletonSeparatedList(simple.Parameter),
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters,
            _ => default,
        };

        private static string GetParameterName(ParameterSyntax parameter) => parameter.Identifier.ValueText;

        private static SyntaxNode FirstArgument(InvocationExpressionSyntax call) =>
            (SyntaxNode?)call.ArgumentList.Arguments.FirstOrDefault() ?? call;

        private void ReportCaptures(LambdaExpressionSyntax lambda)
        {
            foreach (var node in lambda.Body.DescendantNodesAndSelf())
            {
                switch (node)
                {
                    case ThisExpressionSyntax:
                        Report(Diagnostics.LambdaCaptures, node, "this");
                        break;
                    case BaseExpressionSyntax:
                        Report(Diagnostics.LambdaCaptures, node, "base");
                        break;
                    case SimpleNameSyntax name when Captures(name, lambda):
                        Report(Diagnostics.LambdaCaptures, name, name.Identifier.ValueText);
                        break;
                }
            }
        }

        private bool Captures(SimpleNameSyntax name, LambdaExpressionSyntax lambda)
        {
            // These name a member of something else, or a parameter of the call they're in.
            switch (name.Parent)
            {
                case MemberAccessExpressionSyntax access when access.Name == name:
                case MemberBindingExpressionSyntax:
                case NameColonSyntax:
                case NameEqualsSyntax:
                case AssignmentExpressionSyntax { Parent: InitializerExpressionSyntax } assignment when assignment.Left == name:
                    return false;
            }

            var info = semantics.GetSymbolInfo(name, cancellationToken);
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            return symbol switch
            {
                ILocalSymbol or IParameterSymbol or IMethodSymbol { MethodKind: MethodKind.LocalFunction } =>
                    !symbol.DeclaringSyntaxReferences.Any(r => lambda.Span.Contains(r.Span)),
                IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol =>
                    !symbol.IsStatic && IsValidatorMember(symbol) && !IsInNameOf(name),
                _ => false,
            };
        }

        private bool IsValidatorMember(ISymbol symbol)
        {
            for (ITypeSymbol? current = validator; current is not null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, symbol.ContainingType?.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInNameOf(SyntaxNode node) =>
            node.Ancestors().Any(a => a is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } });

        /// <summary>Recognizes exactly <c>param.Rule(args)</c>. Anything else gets the generic defaults.</summary>
        private RuleCall? ReadRuleCall(LambdaExpressionSyntax lambda, string valueParameter)
        {
            if (lambda.Body is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call ||
                !access.IsKind(SyntaxKind.SimpleMemberAccessExpression) ||
                access.Expression is not IdentifierNameSyntax receiver ||
                receiver.Identifier.ValueText != valueParameter)
            {
                return null;
            }

            var methodName = access.Name.Identifier.ValueText;
            var arguments = call.ArgumentList.Arguments
                .Select(a => new RuleCallArgument(a.NameColon?.Name.Identifier.ValueText, ReadConstantText(a.Expression)))
                .ToEquatableArray();

            if (semantics.GetSymbolInfo(call, cancellationToken).Symbol is not IMethodSymbol method)
            {
                return new RuleCall(methodName, false, null, null, EquatableArray<string>.Empty, arguments);
            }

            // The reduced form of an extension method has already lost its receiver parameter.
            var definition = method.ReducedFrom ?? method;
            var parameters = method.ReducedFrom is null ? method.Parameters.Skip(1) : method.Parameters;
            var attribute = definition.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass is { Name: "GeneratedRuleAttribute", ContainingNamespace: { Name: "Makoto", ContainingNamespace.IsGlobalNamespace: true } });

            return attribute is { ConstructorArguments.Length: 3 }
                ? new RuleCall(
                    methodName,
                    true,
                    attribute.ConstructorArguments[1].Value as string,
                    attribute.ConstructorArguments[2].Value as string,
                    parameters.Select(p => p.Name).ToEquatableArray(),
                    arguments)
                : new RuleCall(methodName, true, null, null, EquatableArray<string>.Empty, arguments);
        }

        private string? ReadConstantText(ExpressionSyntax expression)
        {
            var constant = semantics.GetConstantValue(expression, cancellationToken);
            if (!constant.HasValue)
            {
                return null;
            }

            return constant.Value switch
            {
                null => "null",
                bool value => value ? "true" : "false",
                System.IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                var value => value.ToString(),
            };
        }

        private string? ReadConstant(InvocationExpressionSyntax call, string method)
        {
            var argument = call.ArgumentList.Arguments.FirstOrDefault();
            if (argument is not null && semantics.GetConstantValue(argument.Expression, cancellationToken) is { HasValue: true, Value: string value })
            {
                return value;
            }

            Report(Diagnostics.NonConstantArgument, FirstArgument(call), method);
            return null;
        }

        private static CollectionKind ClassifyCollection(ITypeSymbol type) => type switch
        {
            IArrayTypeSymbol { IsSZArray: true } => CollectionKind.Array,
            INamedTypeSymbol { Name: "List", Arity: 1, ContainingNamespace: { Name: "Generic", ContainingNamespace: { Name: "Collections", ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } } } } => CollectionKind.List,
            _ => CollectionKind.Enumerable,
        };

        private static ValueKind GetValueKind(ITypeSymbol type)
        {
            if (!type.IsValueType)
            {
                return ValueKind.Reference;
            }

            return type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T ? ValueKind.NullableValue : ValueKind.Value;
        }

        private void Report(DiagnosticDescriptor descriptor, SyntaxNode node, params string[] arguments) =>
            diagnostics.Add(DiagnosticInfo.Create(descriptor, node.GetLocation(), arguments));
    }
}
