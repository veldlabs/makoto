using System.Linq;
using System.Text;
using Makoto.Generators.Model;
using Microsoft.CodeAnalysis.CSharp;

namespace Makoto.Generators.Validators;

/// <summary>Writes the code shown in docs/design.md.</summary>
internal static class ValidatorEmitter
{
    private const string Inlined =
        "    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]";

    private const string ServiceCollection = "global::Microsoft.Extensions.DependencyInjection.IServiceCollection";

    private const string TryAddSingleton =
        "global::Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.TryAddSingleton";

    public static string Emit(ValidatorModel validator)
    {
        var code = new StringBuilder();
        AppendHeader(code);

        var blocks = AppendScopes(code, validator.Scopes);

        code.AppendLine($"sealed partial class {validator.ClassName}");
        code.AppendLine("{");

        var requires = validator.Chains.SelectMany(c => c.Steps.OfType<RequireStep>().Select(s => (Chain: c, Step: s))).ToList();
        foreach (var (chain, step) in requires)
        {
            var name = chain.Collection == CollectionKind.None ? chain.Name : "";
            code.AppendLine($"    private static readonly global::Makoto.ValidationFailure s_failure{step.Index} = new({Literal(name)}, {Literal(step.Code!)}, {Literal(step.Message!)});");
        }

        if (requires.Count != 0)
        {
            code.AppendLine();
        }

        var model = validator.ModelType;
        // Check first: valid input is the common case, and the collecting code is several times larger because every
        // rule has to carry on after a failure. Rules are pure, so running them again on invalid input is safe.
        code.AppendLine($"    public override global::Makoto.ValidationResult Validate({model} instance) => Check(instance) ? default : Collect(instance);");
        code.AppendLine();
        code.AppendLine("    [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]");
        code.AppendLine($"    private static global::Makoto.ValidationResult Collect({model} instance)");
        code.AppendLine("    {");
        code.AppendLine("        var failures = default(global::Makoto.FailureCollector);");
        code.AppendLine("        ValidateInto(instance, ref failures);");
        code.AppendLine("        return failures.ToResult();");
        code.AppendLine("    }");
        code.AppendLine();
        code.AppendLine($"    public override bool IsValid({model} instance) => Check(instance);");
        code.AppendLine();
        code.AppendLine("    /// <summary>Short-circuits on the first failure.</summary>");
        code.AppendLine($"    public static bool Check({model} instance)");
        code.AppendLine("    {");
        AppendBody(code, validator, collect: false);
        code.AppendLine();
        code.AppendLine("        return true;");
        code.AppendLine("    }");
        code.AppendLine();
        code.AppendLine("    /// <summary>Collects every failure. Nested paths are only built on the failure path.</summary>");
        code.AppendLine($"    public static void ValidateInto({model} instance, ref global::Makoto.FailureCollector failures)");
        code.AppendLine("    {");
        AppendBody(code, validator, collect: true);
        code.AppendLine("    }");

        foreach (var chain in validator.Chains)
        {
            AppendLambda(code, chain.Selector, chain.SelectedType, $"{model} {chain.Selector.Parameters[0]}");

            foreach (var step in chain.Steps.OfType<RequireStep>())
            {
                foreach (var condition in step.Conditions)
                {
                    AppendPredicate(code, condition, chain, model);
                }

                AppendPredicate(code, step.Predicate, chain, model);
            }
        }

        code.AppendLine("}");
        code.Append('}', blocks);
        return code.ToString();
    }

    public static string EmitRegistrations(EquatableArray<Registration> registrations)
    {
        var code = new StringBuilder();
        AppendHeader(code);
        code.AppendLine("namespace Makoto;");
        code.AppendLine();
        code.AppendLine("internal static class MakotoServiceCollectionExtensions");
        code.AppendLine("{");
        code.AppendLine("    /// <summary>Registers every generated validator in this assembly as a singleton.</summary>");
        code.AppendLine($"    public static {ServiceCollection} AddMakotoValidators(this {ServiceCollection} services)");
        code.AppendLine("    {");

        foreach (var registration in registrations)
        {
            code.AppendLine($"        {TryAddSingleton}<global::Makoto.IValidator<{registration.ModelType}>, {registration.Validator}>(services);");
            code.AppendLine($"        {TryAddSingleton}<{registration.Validator}>(services);");
        }

        code.AppendLine("        return services;");
        code.AppendLine("    }");
        code.AppendLine("}");
        return code.ToString();
    }

    private static void AppendHeader(StringBuilder code)
    {
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("#nullable enable");
        code.AppendLine("#pragma warning disable");
        code.AppendLine();
    }

    /// <summary>
    /// Repeats the source file's namespaces and directives, level by level, since an alias in an outer namespace can
    /// be used by a directive in an inner one. Returns how many blocks are left to close.
    /// </summary>
    private static int AppendScopes(StringBuilder code, EquatableArray<NamespaceScope> scopes)
    {
        var namespaces = scopes.Count(s => s.Name is not null);
        foreach (var scope in scopes)
        {
            if (scope.Name is not null)
            {
                code.AppendLine(namespaces == 1 ? $"namespace {scope.Name};" : $"namespace {scope.Name}");
                code.AppendLine(namespaces == 1 ? "" : "{");
            }

            foreach (var directive in scope.Directives)
            {
                code.AppendLine(directive);
            }

            if (scope.Directives.Length != 0)
            {
                code.AppendLine();
            }
        }

        return namespaces == 1 ? 0 : namespaces;
    }

    private static void AppendBody(StringBuilder code, ValidatorModel validator, bool collect)
    {
        if (validator.ModelIsReference)
        {
            code.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(instance);");
        }

        for (var i = 0; i < validator.Chains.Length; i++)
        {
            if (i != 0 || validator.ModelIsReference)
            {
                code.AppendLine();
            }

            if (validator.Chains[i].Collection == CollectionKind.None)
            {
                AppendProperty(code, validator.Chains[i], i, collect);
            }
            else
            {
                AppendEach(code, validator.Chains[i], i, collect);
            }
        }
    }

    private static void AppendProperty(StringBuilder code, RuleChain chain, int index, bool collect)
    {
        const string Indent = "        ";
        var value = $"value{index}";
        var stops = collect && chain.StopOnFailure && chain.Steps.Length > 1;

        code.AppendLine($"{Indent}var {value} = {chain.Selector.Name}(instance);");
        if (stops)
        {
            code.AppendLine($"{Indent}var chain{index} = failures.Count;");
        }

        AppendSteps(code, Indent, chain, index, value, collect, stops ? $"chain{index}" : null);
    }

    private static void AppendEach(StringBuilder code, RuleChain chain, int index, bool collect)
    {
        var items = $"items{index}";
        var item = $"item{index}";
        var position = $"index{index}";
        var start = $"start{index}";
        var isEnumerable = chain.Collection is CollectionKind.Enumerable or CollectionKind.ImmutableArray;
        var nullable = chain.CollectionValueKind != ValueKind.Value;
        var collection = Unwrapped(items, chain.CollectionValueKind);

        // A default ImmutableArray, which a deserializer leaves behind for an omitted property, throws from foreach
        // and counts as empty here.
        var guard = Join([
            nullable ? $"{items} is not null" : null,
            chain.Collection == CollectionKind.ImmutableArray ? $"!{collection}.IsDefault" : null,
        ]);

        var indent = "        ";
        code.AppendLine($"{indent}var {items} = {chain.Selector.Name}(instance);");
        if (guard.Length != 0)
        {
            code.AppendLine($"{indent}if ({guard})");
            code.AppendLine($"{indent}{{");
            indent += "    ";
        }

        if (isEnumerable)
        {
            if (collect)
            {
                code.AppendLine($"{indent}var {position} = 0;");
            }

            code.AppendLine($"{indent}foreach (var {item} in {collection})");
        }
        else if (chain.Collection == CollectionKind.Indexable)
        {
            // Indexing an IList<T> or IReadOnlyList<T> is an interface call per element, foreach would box its enumerator.
            code.AppendLine($"{indent}for (var {position} = 0; {position} < {collection}.Count; {position}++)");
        }
        else
        {
            var span = chain.Collection == CollectionKind.List
                ? $"global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan({items})"
                : $"new global::System.ReadOnlySpan<{chain.ValueType}>({items})";

            code.AppendLine($"{indent}var span{index} = {span};");
            code.AppendLine($"{indent}for (var {position} = 0; {position} < span{index}.Length; {position}++)");
        }

        code.AppendLine($"{indent}{{");

        var body = indent + "    ";
        if (chain.Collection == CollectionKind.Indexable)
        {
            code.AppendLine($"{body}var {item} = {collection}[{position}];");
        }
        else if (!isEnumerable)
        {
            code.AppendLine($"{body}var {item} = span{index}[{position}];");
        }

        if (collect)
        {
            code.AppendLine($"{body}var {start} = failures.Count;");
        }

        AppendSteps(code, body, chain, index, item, collect, collect && chain.StopOnFailure ? start : null);

        if (collect)
        {
            code.AppendLine($"{body}failures.PrefixFrom({start}, {Literal(chain.Name)}, {position});");
            if (isEnumerable)
            {
                code.AppendLine($"{body}{position}++;");
            }
        }

        code.AppendLine($"{indent}}}");
        if (guard.Length != 0)
        {
            code.AppendLine("        }");
        }
    }

    /// <param name="stopStart">The failure count to compare with when the chain stops on its first failure.</param>
    private static void AppendSteps(StringBuilder code, string indent, RuleChain chain, int index, string value, bool collect, string? stopStart)
    {
        for (var i = 0; i < chain.Steps.Length; i++)
        {
            var guard = i != 0 && stopStart is not null ? $"failures.Count == {stopStart}" : null;
            var notNull = chain.ValueKind == ValueKind.Value ? null : $"{value} is not null";

            switch (chain.Steps[i])
            {
                case RequireStep step:
                    var failed = Join([guard, .. step.Conditions.Select(c => Call(c, value)), $"!{Call(step.Predicate, value)}"]);
                    code.AppendLine(collect
                        ? $"{indent}if ({failed}) failures.Add(s_failure{step.Index});"
                        : $"{indent}if ({failed}) return false;");
                    break;

                case ValidateWithStep step when !collect:
                    code.AppendLine($"{indent}if ({Join([notNull, $"!{step.Validator}.Check({Unwrapped(value, chain.ValueKind)})"])}) return false;");
                    break;

                case ValidateWithStep step:
                    AppendValidateInto(code, indent, step, chain, index, value, Join([guard, notNull]));
                    break;
            }
        }
    }

    private static void AppendValidateInto(StringBuilder code, string indent, ValidateWithStep step, RuleChain chain, int index, string value, string condition)
    {
        var call = $"{step.Validator}.ValidateInto({Unwrapped(value, chain.ValueKind)}, ref failures);";

        // In an Each chain the element's PrefixFrom covers these failures as well.
        if (chain.Collection != CollectionKind.None || chain.Name.Length == 0)
        {
            code.AppendLine(condition.Length == 0 ? $"{indent}{call}" : $"{indent}if ({condition}) {call}");
            return;
        }

        if (condition.Length != 0)
        {
            code.AppendLine($"{indent}if ({condition})");
        }

        code.AppendLine($"{indent}{{");
        code.AppendLine($"{indent}    var start{index} = failures.Count;");
        code.AppendLine($"{indent}    {call}");
        code.AppendLine($"{indent}    failures.PrefixFrom(start{index}, {Literal(chain.Name)});");
        code.AppendLine($"{indent}}}");
    }

    private static string Join(string?[] conditions) =>
        string.Join(" && ", conditions.Where(c => c is not null));

    private static string Unwrapped(string value, ValueKind kind) =>
        kind == ValueKind.NullableValue ? $"{value}.GetValueOrDefault()" : value;

    private static string Call(Lambda lambda, string value) =>
        lambda.Parameters.Length == 2 ? $"{lambda.Name}(instance, {value})" : $"{lambda.Name}({value})";

    private static void AppendPredicate(StringBuilder code, Lambda lambda, RuleChain chain, string model)
    {
        var parameters = lambda.Parameters.Length == 2
            ? $"{model} {lambda.Parameters[0]}, {chain.ValueType} {lambda.Parameters[1]}"
            : $"{chain.ValueType} {lambda.Parameters[0]}";

        AppendLambda(code, lambda, "bool", parameters);
    }

    private static void AppendLambda(StringBuilder code, Lambda lambda, string returnType, string parameters)
    {
        code.AppendLine();
        code.AppendLine(Inlined);

        var signature = $"    private static {returnType} {lambda.Name}({parameters})";
        if (lambda.IsBlock)
        {
            code.AppendLine(signature);
            code.AppendLine($"    {lambda.Body}");
        }
        else
        {
            code.AppendLine($"{signature} => {lambda.Body};");
        }
    }

    private static string Literal(string value) =>
        SymbolDisplay.FormatLiteral(value, quote: true);
}
