using System;
using System.Linq;
using System.Text;
using Makoto.Generators.Model;
using Makoto.Generators.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Makoto.Generators.Validators;

/// <summary>Emits the validation code for [Validator] classes. See docs/design.md.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValidatorGenerator : IIncrementalGenerator
{
    internal const string ParseStep = "Makoto.Validators.Parse";
    internal const string ResolveStep = "Makoto.Validators.Resolve";

    private const string AttributeName = "Makoto.ValidatorAttribute";
    private const string ServiceCollectionName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var rules = RuleDiscovery.Create(context)
            .Select(static (result, _) => result.Rule)
            .Where(static rule => rule is not null)
            .Collect()
            .Select(static (rules, _) => rules.Select(rule => rule!).ToEquatableArray());

        var validators = context.SyntaxProvider
            .ForAttributeWithMetadataName(AttributeName, static (node, _) => node is TypeDeclarationSyntax, ValidatorParser.Parse)
            .WithTrackingName(ParseStep);

        var resolved = validators
            .Combine(rules)
            .Select(static (pair, _) => ValidatorResolver.Resolve(pair.Left, pair.Right))
            .WithTrackingName(ResolveStep);

        context.RegisterSourceOutput(resolved, static (output, result) =>
        {
            foreach (var diagnostic in result.Diagnostics)
            {
                output.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (result.Model is { } model)
            {
                // The escape of a keyword like @class isn't allowed in a hint name.
                var hintName = model.QualifiedName.Replace("global::", "").Replace("@", "");
                output.AddSource($"{hintName}.Validator.g.cs", SourceText.From(ValidatorEmitter.Emit(model), Encoding.UTF8));
            }
        });

        var registrations = resolved
            .Collect()
            .Select(static (results, _) => new RegistrationSet(
                results.Length != 0,
                results.Select(r => r.Model).OfType<ValidatorModel>()
                    .OrderBy(m => m.QualifiedName, StringComparer.Ordinal)
                    .Select(m => new Registration(m.ModelType, m.QualifiedName))
                    .ToEquatableArray()));

        var hasServiceCollection = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName(ServiceCollectionName) is not null);

        context.RegisterSourceOutput(registrations.Combine(hasServiceCollection), static (output, pair) =>
        {
            var (set, hasServices) = pair;
            if (hasServices && set.HasValidators)
            {
                output.AddSource("MakotoServiceCollectionExtensions.g.cs", SourceText.From(ValidatorEmitter.EmitRegistrations(set.Registrations), Encoding.UTF8));
            }
        });
    }
}
