namespace Makoto.Generators.Tests.Validators;

/// <summary>
/// Whether AddMakotoValidators appears. This project has no DI package, so a stub stands in for it. The real thing is
/// covered in Makoto.Tests.
/// </summary>
public class RegistrationTests
{
    private const string FileName = "MakotoServiceCollectionExtensions.g.cs";

    private const string ServiceCollectionStub = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
        }

        namespace Microsoft.Extensions.DependencyInjection.Extensions
        {
            public static class ServiceCollectionDescriptorExtensions
            {
                public static void TryAddSingleton<TService, TImplementation>(IServiceCollection services) { }
                public static void TryAddSingleton<TService>(IServiceCollection services) { }
            }
        }
        """;

    private const string Validators = """
        using Makoto;

        namespace Demo;

        public sealed class Second { }
        public sealed class First { }

        [Validator]
        public sealed partial class SecondValidator : Validator<Second>
        {
            protected override void Configure() { }
        }

        [Validator]
        public sealed partial class FirstValidator : Validator<First>
        {
            protected override void Configure() { }
        }
        """;

    [Fact]
    public void Nothing_is_emitted_without_the_DI_package()
    {
        var result = GeneratorHarness.Run(Validators);

        ValidatorHarness.AssertClean(result);
        Assert.DoesNotContain(FileName, result.Sources.Keys);
    }

    [Fact]
    public void One_registration_pair_per_validator_in_a_stable_order()
    {
        var result = GeneratorHarness.Run(Validators, ServiceCollectionStub);

        ValidatorHarness.AssertClean(result);

        var code = result.Sources[FileName];
        Assert.Contains("namespace Makoto;", code);
        Assert.Contains("internal static class MakotoServiceCollectionExtensions", code);
        Assert.Contains("TryAddSingleton<global::Makoto.IValidator<global::Demo.First>, global::Demo.FirstValidator>(services);", code);
        Assert.Contains("TryAddSingleton<global::Demo.FirstValidator>(services);", code);
        Assert.Contains("TryAddSingleton<global::Makoto.IValidator<global::Demo.Second>, global::Demo.SecondValidator>(services);", code);
        Assert.True(code.IndexOf("Demo.FirstValidator", StringComparison.Ordinal) < code.IndexOf("Demo.SecondValidator", StringComparison.Ordinal));
    }

    [Fact]
    public void An_assembly_without_validators_gets_no_extension()
    {
        var result = GeneratorHarness.Run(ServiceCollectionStub);

        ValidatorHarness.AssertClean(result);
        Assert.DoesNotContain(FileName, result.Sources.Keys);
    }

    [Fact]
    public void A_validator_with_errors_is_left_out_but_the_method_stays()
    {
        var result = GeneratorHarness.Run(ServiceCollectionStub, """
            using Makoto;

            public sealed class M { }

            [Validator]
            public sealed class Broken : Validator<M>
            {
                protected override void Configure() { }
            }
            """);

        Assert.Equal(["MKO010"], result.Ids());
        Assert.Contains("AddMakotoValidators", result.Sources[FileName]);
        Assert.DoesNotContain("Broken", result.Sources[FileName]);
    }
}
