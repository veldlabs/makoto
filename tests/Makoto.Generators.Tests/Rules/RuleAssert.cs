using System.Reflection;

namespace Makoto.Generators.Tests.Rules;

internal static class RuleAssert
{
    /// <summary>Runs the generators and requires a clean result, including the user code that calls the extensions.</summary>
    public static GeneratorHarness.Result Compiles(params string[] sources)
    {
        var result = GeneratorHarness.Run(sources);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompileErrors);
        return result;
    }

    /// <summary>The single diagnostic of a rule that must not be emitted.</summary>
    public static void Rejected(string id, string source, string messagePart)
    {
        var result = GeneratorHarness.Run(source);
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Contains(messagePart, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(result.Sources);
    }

    /// <summary>The first overload of an extension, the one on the rule's own value type. Generic methods are closed over <paramref name="typeArguments"/>.</summary>
    public static MethodInfo Extension(Assembly assembly, string extensionClass, string name, params Type[] typeArguments)
    {
        var method = assembly.GetType(extensionClass, throwOnError: true)!.GetMethods().First(m => m.Name == name);
        return typeArguments.Length == 0 ? method : method.MakeGenericMethod(typeArguments);
    }

    /// <summary>A rule's generated source, by hint name without its namespace.</summary>
    public static string RuleSource(this GeneratorHarness.Result result, string hintName) =>
        result.Sources.Single(s => s.Key == hintName || s.Key.EndsWith($".{hintName}")).Value;
}
