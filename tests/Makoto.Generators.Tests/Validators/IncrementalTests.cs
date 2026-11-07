using Makoto.Generators.Validators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Makoto.Generators.Tests.Validators;

/// <summary>Editing the compilation must only redo work for validators whose output can change.</summary>
public class IncrementalTests
{
    private const string Rule = """
        using Makoto;

        namespace Demo;

        [ValidationRule(Message = "'{PropertyName}' is not an email address.")]
        public sealed class IsEmailValidationRule : ValidationRule<string>
        {
            public override bool Evaluate(string value) => value.Contains('@');
        }
        """;

    private const string Validators = """
        using Makoto;

        namespace Demo;

        public sealed class Signup { public string? Email { get; set; } }

        [Validator]
        public sealed partial class SignupValidator : Validator<Signup>
        {
            protected override void Configure() => Property(x => x.Email).Require(x => x.IsEmail()).When(x => x is not null);
        }
        """;

    private static readonly IncrementalStepRunReason[] Unchanged =
        [IncrementalStepRunReason.Cached, IncrementalStepRunReason.Unchanged];

    private static (GeneratorDriver Driver, CSharpCompilation Compilation) FirstRun()
    {
        var compilation = ValidatorHarness.CreateCompilation([], Rule, Validators);
        return (ValidatorHarness.CreateDriver(trackSteps: true).RunGenerators(compilation), compilation);
    }

    private static GeneratorRunResult ValidatorRun(GeneratorDriver driver) =>
        driver.GetRunResult().Results.Single(r => r.Generator.GetGeneratorType() == typeof(ValidatorGenerator));

    private static IncrementalStepRunReason[] Reasons(GeneratorRunResult run, string step) =>
        run.TrackedSteps[step].SelectMany(s => s.Outputs).Select(o => o.Reason).ToArray();

    /// <summary>What the two RegisterSourceOutput calls saw, which decides whether emission runs.</summary>
    private static IncrementalStepRunReason[] EmitReasons(GeneratorRunResult run) =>
        run.TrackedOutputSteps.SelectMany(s => s.Value).SelectMany(s => s.Outputs).Select(o => o.Reason).ToArray();

    private static SyntaxTree Tree(CSharpCompilation compilation, string marker) =>
        compilation.SyntaxTrees.Single(t => t.ToString().Contains(marker));

    private static CSharpCompilation Replace(CSharpCompilation compilation, string marker, string text) =>
        compilation.ReplaceSyntaxTree(Tree(compilation, marker), CSharpSyntaxTree.ParseText(text, ValidatorHarness.ParseOptions));

    [Fact]
    public void The_first_run_emits()
    {
        var run = ValidatorRun(FirstRun().Driver);

        Assert.Equal([IncrementalStepRunReason.New], Reasons(run, ValidatorGenerator.ResolveStep));
        Assert.All(EmitReasons(run), reason => Assert.Equal(IncrementalStepRunReason.New, reason));
    }

    [Fact]
    public void An_unrelated_new_file_does_not_emit_again()
    {
        var (driver, compilation) = FirstRun();

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }", ValidatorHarness.ParseOptions));
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.All(Reasons(run, ValidatorGenerator.ResolveStep), reason => Assert.Contains(reason, Unchanged));
        Assert.NotEmpty(EmitReasons(run));
        Assert.All(EmitReasons(run), reason => Assert.Contains(reason, Unchanged));
    }

    [Fact]
    public void An_unrelated_edit_to_another_file_does_not_emit_again()
    {
        var (first, compilation) = FirstRun();
        var withOther = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }", ValidatorHarness.ParseOptions));
        var driver = first.RunGenerators(withOther);

        var edited = Replace(withOther, "Unrelated", "public class Unrelated { public int Changed; }");
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.All(EmitReasons(run), reason => Assert.Contains(reason, Unchanged));
    }

    [Fact]
    public void Moving_a_validator_down_its_file_does_not_emit_again()
    {
        var (driver, compilation) = FirstRun();

        var edited = Replace(compilation, "SignupValidator", $"// A comment that shifts every position.\n\n{Validators}");
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.All(Reasons(run, ValidatorGenerator.ParseStep), reason => Assert.Contains(reason, Unchanged));
        Assert.All(EmitReasons(run), reason => Assert.Contains(reason, Unchanged));
    }

    [Fact]
    public void Changing_a_rule_body_does_not_emit_again()
    {
        var (driver, compilation) = FirstRun();

        var edited = Replace(compilation, "IsEmailValidationRule", Rule.Replace("value.Contains('@')", "value.Length > 3"));
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.All(EmitReasons(run), reason => Assert.Contains(reason, Unchanged));
    }

    [Fact]
    public void Changing_a_rule_message_emits_again()
    {
        var (driver, compilation) = FirstRun();

        var edited = Replace(compilation, "IsEmailValidationRule", Rule.Replace("is not an email address", "is no email"));
        var after = driver.RunGenerators(edited);
        var run = ValidatorRun(after);

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(run, ValidatorGenerator.ResolveStep));
        Assert.Contains("is no email", run.GeneratedSources.Single(s => s.HintName.Contains("SignupValidator")).SourceText.ToString());
    }

    [Fact]
    public void Changing_a_lambda_emits_again()
    {
        var (driver, compilation) = FirstRun();

        var edited = Replace(compilation, "SignupValidator", Validators.Replace("x is not null", "x != null"));
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.Contains(IncrementalStepRunReason.Modified, Reasons(run, ValidatorGenerator.ParseStep));
        Assert.Contains("x != null", run.GeneratedSources.Single(s => s.HintName.Contains("SignupValidator")).SourceText.ToString());
    }

    [Fact]
    public void Editing_one_validator_leaves_the_other_alone()
    {
        const string Second = """
            using Makoto;

            namespace Demo;

            [Validator]
            public sealed partial class SecondValidator : Validator<Signup>
            {
                protected override void Configure() => Property(x => x.Email).Require(x => x is not null);
            }
            """;

        var compilation = ValidatorHarness.CreateCompilation([], Rule, Validators, Second);
        var driver = ValidatorHarness.CreateDriver(trackSteps: true).RunGenerators(compilation);

        var edited = Replace(compilation, "SecondValidator", Second.Replace("x is not null", "x != null"));
        var reasons = Reasons(ValidatorRun(driver.RunGenerators(edited)), ValidatorGenerator.ResolveStep);

        Assert.Equal(1, reasons.Count(r => r == IncrementalStepRunReason.Modified));
        Assert.Equal(1, reasons.Count(r => Unchanged.Contains(r)));
    }

    [Fact]
    public void Diagnostics_survive_a_cached_run()
    {
        var compilation = ValidatorHarness.CreateCompilation([], Validators.Replace("sealed partial class SignupValidator", "sealed class SignupValidator"));
        var driver = ValidatorHarness.CreateDriver(trackSteps: true).RunGenerators(compilation);

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { }", ValidatorHarness.ParseOptions));
        var run = ValidatorRun(driver.RunGenerators(edited));

        Assert.Equal("MKO010", Assert.Single(run.Diagnostics).Id);
    }

    [Fact]
    public void Models_hold_no_symbols_or_syntax()
    {
        var run = ValidatorRun(FirstRun().Driver);

        var model = (ValidatorResult)run.TrackedSteps[ValidatorGenerator.ParseStep].Single().Outputs.Single().Value;
        var again = (ValidatorResult)ValidatorRun(FirstRun().Driver).TrackedSteps[ValidatorGenerator.ParseStep].Single().Outputs.Single().Value;

        // Two separate compilations of the same text: only value equality can make these equal.
        Assert.Equal(model, again);
        Assert.Equal(model.GetHashCode(), again.GetHashCode());
    }
}
