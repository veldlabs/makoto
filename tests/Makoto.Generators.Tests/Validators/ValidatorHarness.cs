using System.Collections.Immutable;
using System.Reflection;
using Makoto.Generators.Rules;
using Makoto.Generators.Validators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Makoto.Generators.Tests.Validators;

/// <summary>Helpers on top of <see cref="GeneratorHarness"/> for validators.</summary>
internal static class ValidatorHarness
{
    public static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    /// <summary>Compiles sources that are expected to be fine and loads the result.</summary>
    public static Assembly Load(params string[] sources)
    {
        var result = GeneratorHarness.Run(sources);
        AssertClean(result);
        return result.Load();
    }

    public static void AssertClean(GeneratorHarness.Result result)
    {
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompileErrors);
    }

    /// <summary>
    /// Validates what <c>Samples.{sample}()</c> returns and renders each failure as <c>path|code|message</c>. Also holds
    /// IsValid and Check to the same verdict, so every behavior test covers that.
    /// </summary>
    public static string[] Failures(this Assembly assembly, string validatorName, string sample)
    {
        var type = assembly.GetType(validatorName) ?? throw new InvalidOperationException($"No type {validatorName}.");
        var model = assembly.GetType("Samples")!.GetMethod(sample)!.Invoke(null, null);
        var validator = Activator.CreateInstance(type);

        var result = (ValidationResult)type.GetMethod("Validate")!.Invoke(validator, [model])!;
        Assert.Equal(result.IsValid, (bool)type.GetMethod("IsValid")!.Invoke(validator, [model])!);
        Assert.Equal(result.IsValid, (bool)type.GetMethod("Check")!.Invoke(null, [model])!);

        return result.Failures.ToArray().Select(f => $"{f.PropertyName}|{f.ErrorCode}|{f.Message}").ToArray();
    }

    /// <summary>The paths of the failures.</summary>
    public static string[] Paths(this Assembly assembly, string validatorName, string sample) =>
        assembly.Failures(validatorName, sample).Select(f => f.Split('|')[0]).ToArray();

    public static string Generated(this GeneratorHarness.Result result, string validatorName) =>
        result.Sources[$"{validatorName}.Validator.g.cs"];

    public static IEnumerable<string> Ids(this GeneratorHarness.Result result) =>
        result.GeneratorDiagnostics.Select(d => d.Id);

    /// <summary>Like <see cref="GeneratorHarness.Run"/>, with extra references.</summary>
    public static GeneratorHarness.Result Run(IEnumerable<MetadataReference> references, params string[] sources)
    {
        var compilation = CreateCompilation(references, sources);
        var driver = CreateDriver(trackSteps: false).RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        var generated = driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .ToImmutableDictionary(s => s.HintName, s => s.SourceText.ToString());

        return new GeneratorHarness.Result((CSharpCompilation)output, diagnostics, generated);
    }

    public static CSharpCompilation CreateCompilation(IEnumerable<MetadataReference> references, params string[] sources) =>
        CSharpCompilation.Create(
            "Makoto.Generated.Test",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)),
            FrameworkReferences().Concat(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public static GeneratorDriver CreateDriver(bool trackSteps) =>
        CSharpGeneratorDriver.Create(
            [new RuleGenerator().AsSourceGenerator(), new ValidatorGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: trackSteps));

    /// <summary>Compiles sources into a reference, for rules and validators from another assembly.</summary>
    public static MetadataReference CompileToReference(params string[] sources)
    {
        var result = GeneratorHarness.Run(sources);
        AssertClean(result);

        using var stream = new MemoryStream();
        var emit = result.Compilation.WithAssemblyName("Makoto.Generated.Library").Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> FrameworkReferences()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trusted
            .Where(path => Path.GetFileName(path) is var name && (name.StartsWith("System.") || name is "netstandard.dll" or "mscorlib.dll" or "Makoto.dll"))
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
