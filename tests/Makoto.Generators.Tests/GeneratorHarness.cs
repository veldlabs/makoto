using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Makoto.Generators.Rules;
using Makoto.Generators.Validators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Makoto.Generators.Tests;

/// <summary>Runs the generators over source text, for asserting on diagnostics, output and behavior.</summary>
internal static class GeneratorHarness
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    public sealed record Result(
        CSharpCompilation Compilation,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableDictionary<string, string> Sources)
    {
        /// <summary>All errors from compiling the user code together with the generated code.</summary>
        public IEnumerable<Diagnostic> CompileErrors =>
            Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);

        /// <summary>Emits to memory and loads the assembly, failing on compile errors.</summary>
        public Assembly Load()
        {
            using var stream = new MemoryStream();
            var emit = Compilation.Emit(stream);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            stream.Position = 0;
            return new AssemblyLoadContext(null, isCollectible: true).LoadFromStream(stream);
        }
    }

    public static Result Run(params string[] sources)
    {
        var compilation = CSharpCompilation.Create(
            "Makoto.Generated.Test",
            sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)),
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new RuleGenerator().AsSourceGenerator(), new ValidatorGenerator().AsSourceGenerator()],
            parseOptions: ParseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        var generated = driver.GetRunResult().Results
            .SelectMany(r => r.GeneratedSources)
            .ToImmutableDictionary(s => s.HintName, s => s.SourceText.ToString());

        return new Result((CSharpCompilation)output, diagnostics, generated);
    }

    private static IEnumerable<MetadataReference> References()
    {
        var trusted = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
        return trusted
            .Where(path => Path.GetFileName(path) is var name && (name.StartsWith("System.") || name is "netstandard.dll" or "mscorlib.dll" or "Makoto.dll"))
            .Select(path => MetadataReference.CreateFromFile(path));
    }
}
