using Microsoft.CodeAnalysis;

namespace Makoto.Generators.Tests.Validators;

public class DiagnosticTests
{
    private const string Model = """
        using System;
        using System.Collections.Generic;
        using Makoto;

        public sealed class Address { public string? City { get; set; } }

        public sealed class M
        {
            public string? Name { get; set; }
            public int Age { get; set; }
            public Address? Address { get; set; }
            public List<Address>? Addresses { get; set; }
            public string? GetName() => Name;
            public string?[] Names { get; set; } = [];
        }

        [Validator]
        public sealed partial class AddressValidator : Validator<Address>
        {
            protected override void Configure() { }
        }

        [Validator]
        public sealed partial class OtherValidator : Validator<M>
        {
            protected override void Configure() => Property(x => x.Name).Require(x => x is not null);
        }

        public sealed class HandWrittenValidator : Validator<Address>
        {
            protected override void Configure() { }
            public override ValidationResult Validate(Address instance) => default;
            public override bool IsValid(Address instance) => true;
        }
        """;

    private static GeneratorHarness.Result Configure(string statements, string members = "") =>
        GeneratorHarness.Run(Model, $$"""
            using System;
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<M>
            {
                {{members}}

                protected override void Configure()
                {
                    {{statements}}
                }
            }
            """);

    private static Diagnostic Single(GeneratorHarness.Result result, string id)
    {
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        return diagnostic;
    }

    // DiagnosticInfo rebuilds locations from a path and a span, so they have no tree. Model is the first tree here.
    private static string Text(GeneratorHarness.Result result, Diagnostic diagnostic) =>
        result.Compilation.SyntaxTrees.ElementAt(1).GetText().ToString(diagnostic.Location.SourceSpan);

    /// <summary>The validator is skipped, the others still generate, and the user sees CS0534 at most.</summary>
    private static void AssertSkipped(GeneratorHarness.Result result)
    {
        Assert.DoesNotContain("V.Validator.g.cs", result.Sources.Keys);
        Assert.Contains("OtherValidator.Validator.g.cs", result.Sources.Keys);
        Assert.Contains("CS0534", result.CompileErrors.Select(e => e.Id));
    }

    [Fact]
    public void MKO010_not_partial()
    {
        var result = GeneratorHarness.Run(Model, """
            using Makoto;

            [Validator]
            public sealed class V : Validator<M>
            {
                protected override void Configure() { }
            }
            """);

        var diagnostic = Single(result, "MKO010");
        Assert.Equal("V", Text(result, diagnostic));
        Assert.Contains("'V'", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Theory]
    [InlineData("[Validator] public partial class V<T> : Validator<M> { protected override void Configure() { } }")]
    [InlineData("[Validator] public partial class V : Base { protected override void Configure() { } } public abstract class Base : Validator<M>;")]
    [InlineData("[Validator] public partial class V { }")]
    [InlineData("[Validator] public partial class V : object { }")]
    [InlineData("public partial class Outer { [Validator] public partial class V : Validator<M> { protected override void Configure() { } } }")]
    [InlineData("[Validator] public abstract partial class V : Validator<M> { protected override void Configure() { } }")]
    [InlineData("[Validator] file partial class V : Validator<M> { protected override void Configure() { } }")]
    [InlineData("[Validator] public partial record V { }")]
    public void MKO011_unsupported_shape(string source)
    {
        var result = GeneratorHarness.Run(Model, $"using Makoto;\n{source}");

        Assert.Equal("V", Text(result, Single(result, "MKO011")));
        Assert.DoesNotContain(result.Sources.Keys, k => k.Contains("V.Validator") && !k.Contains("AddressValidator") && !k.Contains("OtherValidator"));
    }

    [Theory]
    [InlineData("var limit = 3;", "var limit = 3;")]
    [InlineData("if (DateTime.Now.Year > 2000) { }", "if (DateTime.Now.Year > 2000) { }")]
    [InlineData("foreach (var i in new[] { 1 }) Property(x => x.Name).Require(x => true);", "foreach (var i in new[] { 1 }) Property(x => x.Name).Require(x => true);")]
    [InlineData("return;", "return;")]
    [InlineData("Console.WriteLine();", "Console.WriteLine()")]
    [InlineData("Other().Require(x => true);", "Other().Require(x => true)")]
    [InlineData("Property(x => x.Name).Other();", "Property(x => x.Name).Other()")]
    public void MKO012_unsupported_statement(string statement, string reported)
    {
        var result = Configure(statement);

        Assert.Equal(reported, Text(result, Single(result, "MKO012")));
        AssertSkipped(result);
    }

    [Fact]
    public void MKO012_a_method_that_hides_Property_is_no_chain()
    {
        var result = Configure(
            "Property(x => x.Name);",
            "private static void Property(Func<M, string?> selector) { }");

        Single(result, "MKO012");
    }

    [Theory]
    [InlineData("var limit = 3; Property(x => x.Age).Require(x => x > limit);", "limit")]
    [InlineData("const int limit = 3; Property(x => x.Age).Require(x => x > limit);", "limit")]
    [InlineData("int Local() => 1; Property(x => x.Age).Require(x => x > Local());", "Local")]
    public void MKO013_locals(string statements, string name)
    {
        var result = Configure(statements);

        var capture = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "MKO013");
        Assert.Equal(name, Text(result, capture));
        Assert.Contains($"'{name}'", capture.GetMessage());
        AssertSkipped(result);
    }

    [Theory]
    [InlineData("x => x > _field", "_field")]
    [InlineData("x => x > Instance", "Instance")]
    [InlineData("x => Helper(x)", "Helper")]
    [InlineData("x => x > this._field", "this")]
    [InlineData("x => x == GetHashCode()", "GetHashCode")]
    [InlineData("x => x > base.GetHashCode()", "base")]
    public void MKO013_instance_members(string lambda, string name)
    {
        var result = Configure(
            $"Property(x => x.Age).Require({lambda});",
            """
            private readonly int _field = 1;
            private int Instance => 1;
            private bool Helper(int value) => true;
            """);

        Assert.Equal(name, Text(result, Single(result, "MKO013")));
        AssertSkipped(result);
    }

    [Fact]
    public void MKO013_in_a_when()
    {
        var result = Configure("Property(x => x.Age).Require(x => true).When(x => x > Instance);", "private int Instance => 1;");

        Assert.Equal("Instance", Text(result, Single(result, "MKO013")));
    }

    [Fact]
    public void MKO013_inside_a_call_that_does_not_bind()
    {
        var result = Configure(
            "Property(x => x.Name).Require(x => x.IsSomething(Instance));",
            "private int Instance => 1;");

        Assert.Equal("Instance", Text(result, Single(result, "MKO013")));
    }

    [Fact]
    public void MKO013_on_an_instance_method_that_takes_a_call_that_does_not_bind()
    {
        var result = Configure(
            "Property(x => x.Name).Require(x => Helper(x.IsSomething()));",
            "private bool Helper(bool value) => value;");

        Assert.Equal("Helper", Text(result, Single(result, "MKO013")));
    }

    [Theory]
    [InlineData("Property(x => x.Name).Require(Check);", "Check", "Require")]
    [InlineData("Property(x => x.Name).Require(s_check);", "s_check", "Require")]
    [InlineData("Property(x => x.Name).Require(delegate (string? x) { return true; });", "delegate (string? x) { return true; }", "Require")]
    [InlineData("Property(x => x.Name).Require(x => true).When(Check);", "Check", "When")]
    [InlineData("Property(Select);", "Select", "Property")]
    [InlineData("Each(Selects);", "Selects", "Each")]
    public void MKO014_not_a_lambda(string statement, string reported, string method)
    {
        var result = Configure(
            statement,
            """
            private static readonly Func<string?, bool> s_check = x => true;
            private static bool Check(string? value) => true;
            private static string? Select(M model) => model.Name;
            private static string?[] Selects(M model) => model.Names;
            """);

        var diagnostic = Single(result, "MKO014");
        Assert.Equal(reported, Text(result, diagnostic));
        Assert.Contains($"'{method}'", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Theory]
    [InlineData("Property(x => x)", "x => x")]
    [InlineData("Property(x => x!)", "x => x!")]
    [InlineData("Property(x => x.GetName())", "x => x.GetName()")]
    [InlineData("Property(x => x.Names[0])", "x => x.Names[0]")]
    [InlineData("Property(x => x.Name ?? \"\")", "x => x.Name ?? \"\"")]
    [InlineData("Property(x => { return x.Name; })", "x => { return x.Name; }")]
    [InlineData("Property(x => s_other.Name)", "x => s_other.Name")]
    [InlineData("Each(x => x.Names.AsReadOnly())", "x => x.Names.AsReadOnly()")]
    public void MKO015_invalid_selector(string root, string reported)
    {
        var result = Configure($"{root}.Require(x => true);", "private static readonly M s_other = new();");

        Assert.Equal(reported, Text(result, Single(result, "MKO015")));
        AssertSkipped(result);
    }

    [Theory]
    [InlineData("WithMessage", "Text()")]
    [InlineData("WithCode", "Text()")]
    [InlineData("Named", "Text()")]
    [InlineData("WithMessage", "s_text")]
    [InlineData("Named", "null!")]
    public void MKO016_not_constant(string method, string argument)
    {
        var result = Configure(
            $"Property(x => x.Name).Require(x => true).{method}({argument});",
            """
            private static readonly string s_text = "";
            private static string Text() => "";
            """);

        var diagnostic = Single(result, "MKO016");
        Assert.Equal(argument, Text(result, diagnostic));
        Assert.Contains($"'{method}'", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Fact]
    public void MKO016_after_a_require_that_does_not_bind()
    {
        var result = Configure(
            "Property(x => x.Name).Require(x => x.IsEmail()).WithMessage(s_text);",
            "private static readonly string s_text = \"\";");

        Single(result, "MKO016");
    }

    [Theory]
    [InlineData("Property(x => x.Name).ValidateWith<AddressValidator>();", "AddressValidator", "string")]
    [InlineData("Property(x => x.Address).ValidateWith<OtherValidator>();", "OtherValidator", "Address")]
    [InlineData("Each(x => x.Addresses).ValidateWith<OtherValidator>();", "OtherValidator", "Address")]
    [InlineData("Property(x => x.Address).ValidateWith<object>();", "object", "Address")]
    public void MKO017_mismatch(string statement, string reported, string model)
    {
        var result = Configure(statement);

        var diagnostic = Single(result, "MKO017");
        Assert.Equal(reported, Text(result, diagnostic));
        Assert.Contains($"'{model}", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Theory]
    [InlineData("Property(x => x.Address).ValidateWith<AddressValidator>();")]
    [InlineData("Property(x => x.Address!).ValidateWith<AddressValidator>();")]
    [InlineData("Each(x => x.Addresses).ValidateWith<AddressValidator>();")]
    public void MKO017_ignores_nullability(string statement)
    {
        ValidatorHarness.AssertClean(Configure(statement));
    }

    /// <summary>Without the attribute there is no generated Check or ValidateInto to call, so this would be CS0117 in the generated file.</summary>
    [Theory]
    [InlineData("Property(x => x.Address).ValidateWith<HandWrittenValidator>();")]
    [InlineData("Each(x => x.Addresses).ValidateWith<HandWrittenValidator>();")]
    public void MKO019_not_a_generated_validator(string statement)
    {
        var result = Configure(statement);

        var diagnostic = Single(result, "MKO019");
        Assert.Equal("HandWrittenValidator", Text(result, diagnostic));
        Assert.Contains("'HandWrittenValidator'", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Fact]
    public void MKO019_skips_the_step_so_a_modifier_after_it_is_still_reported()
    {
        var result = Configure("""Property(x => x.Address).ValidateWith<HandWrittenValidator>().WithCode("a");""");

        Assert.Equal(["MKO018", "MKO019"], result.Ids().Order());
    }

    [Theory]
    [InlineData("Property(x => x.Name).When(x => true).Require(x => true);", "When")]
    [InlineData("""Property(x => x.Name).WithMessage("a").Require(x => true);""", "WithMessage")]
    [InlineData("""Property(x => x.Name).WithCode("a");""", "WithCode")]
    [InlineData("""Each(x => x.Names).WithCode("a");""", "WithCode")]
    [InlineData("""Property(x => x.Name).Named("a").StopOnFailure().When((m, x) => true);""", "When")]
    [InlineData("Property(x => x.Address).ValidateWith<AddressValidator>().When(x => true);", "When")]
    [InlineData("""Property(x => x.Address).Require(x => true).ValidateWith<AddressValidator>().WithCode("a");""", "WithCode")]
    public void MKO018_modifier_without_require(string statement, string method)
    {
        var result = Configure(statement);

        var diagnostic = Single(result, "MKO018");
        Assert.Equal(method, Text(result, diagnostic));
        Assert.Contains($"'{method}'", diagnostic.GetMessage());
        AssertSkipped(result);
    }

    [Fact]
    public void Modifiers_can_follow_chain_level_calls()
    {
        ValidatorHarness.AssertClean(Configure("""Property(x => x.Name).Require(x => true).StopOnFailure().Named("a").WithCode("b").When(x => true);"""));
    }

    [Fact]
    public void Every_problem_in_a_configure_is_reported()
    {
        var result = Configure("""
            var a = 1;
            Property(x => x).Require(x => true);
            Property(x => x.Age).When(x => true).Require(x => x > a);
            """);

        Assert.Equal(["MKO012", "MKO013", "MKO015", "MKO018"], result.Ids().Order());
    }

    [Theory]
    [InlineData("Property(")]
    [InlineData("Property(x")]
    [InlineData("Property(x =>")]
    [InlineData("Property(x => x.")]
    [InlineData("Property(x => x.Name")]
    [InlineData("Property(x => x.Name).")]
    [InlineData("Property(x => x.Name).Require(")]
    [InlineData("Property(x => x.Name).Require(x")]
    [InlineData("Property(x => x.Name).Require(x =>")]
    [InlineData("Property(x => x.Name).Require(x => x.")]
    [InlineData("Property(x => x.Name).Require(x => x.Length > ).")]
    [InlineData("Property(x => x.Name).WithMessage(")]
    [InlineData("Property(x => x.Name).ValidateWith<")]
    [InlineData("Property(x => x.Name).ValidateWith<>();")]
    [InlineData("Property(x => x.Name).ValidateWith();")]
    [InlineData("Property(x => x.Name).ValidateWith<Missing>();")]
    [InlineData("Property();")]
    [InlineData("Property(x => x.Missing).Require(x => true);")]
    [InlineData("Property(x => x.Name).Require();")]
    [InlineData("Property(x => x.Name).Require((a, b, c) => true);")]
    [InlineData("Property(x => x.Name).Require(() => true);")]
    [InlineData("Property(x => x.Name).When();")]
    [InlineData("Property(x => x.Name).WithMessage();")]
    [InlineData("Property(x => x.Name).WithMessage(1);")]
    [InlineData("Property((a, b) => a.Name).Require(x => true);")]
    [InlineData("Each(x => x.Age).Require(x => true);")]
    [InlineData("Each(x => x.Missing).Require(x => true);")]
    [InlineData("Property")]
    [InlineData("Property.Require(x => true);")]
    [InlineData("new V().Property(x => x.Name);")]
    [InlineData("()")]
    public void Incomplete_code_never_crashes_the_generator(string statement)
    {
        var result = Configure(statement);

        // A generator that throws surfaces as CS8785, and takes every other validator down with it.
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id is "CS8785" or "AD0001");
        Assert.Contains("OtherValidator.Validator.g.cs", result.Sources.Keys);
    }

    [Theory]
    [InlineData("[Validator] public sealed partial class V : Validator<Missing> { protected override void Configure() { } }")]
    [InlineData("[Validator] public sealed partial class V : Validator<> { }")]
    [InlineData("[Validator] public sealed partial class V : Validator<M> { protected override void Configure(); }")]
    [InlineData("[Validator] public sealed partial class V : Validator<M> { protected override void Configure(int a) { } }")]
    [InlineData("[Validator] public sealed partial class V : Validator<M> { }")]
    [InlineData("[Validator] public sealed partial class V")]
    [InlineData("[Validator] struct S { }")]
    [InlineData("[Validator] interface I { }")]
    [InlineData("[Validator] [Validator] public sealed partial class V : Validator<M> { protected override void Configure() { } }")]
    public void Incomplete_declarations_never_crash_the_generator(string source)
    {
        var result = GeneratorHarness.Run(Model, $"using Makoto;\n{source}");

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id is "CS8785" or "AD0001");
        Assert.Contains("OtherValidator.Validator.g.cs", result.Sources.Keys);
    }

    [Fact]
    public void The_attribute_on_two_parts_does_not_generate_twice()
    {
        var result = GeneratorHarness.Run(Model, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<M>;

            [Validator]
            public sealed partial class V
            {
                protected override void Configure() { }
            }
            """);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id is "CS8785" or "AD0001");
        Assert.Contains("CS0579", result.CompileErrors.Select(e => e.Id));
    }

    [Fact]
    public void A_missing_Configure_still_generates_the_rest()
    {
        var result = GeneratorHarness.Run(Model, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<M>;
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Equal(["CS0534"], result.CompileErrors.Select(e => e.Id));
    }
}
