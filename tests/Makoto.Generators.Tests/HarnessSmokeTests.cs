namespace Makoto.Generators.Tests;

public class HarnessSmokeTests
{
    [Fact]
    public void Rule_compiles_and_runs()
    {
        var result = GeneratorHarness.Run("""
            using Makoto;
            namespace Demo;

            [ValidationRule]
            public sealed class IsEvenRule : ValidationRule<int>
            {
                public override bool Evaluate(int value) => value % 2 == 0;
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompileErrors);

        var extensions = result.Load().GetType("Demo.IsEvenRuleExtensions")!;
        var isEven = extensions.GetMethod("IsEven", [typeof(int)])!;
        Assert.Equal(true, isEven.Invoke(null, [4]));
    }
}
