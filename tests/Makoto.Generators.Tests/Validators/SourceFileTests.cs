namespace Makoto.Generators.Tests.Validators;

/// <summary>Usings, namespaces and partial parts: the lambdas must bind in the generated file as they did in the source.</summary>
public class SourceFileTests
{
    private const string Library = """
        namespace Lib.Text
        {
            public static class Checks
            {
                public static bool Short(string? value) => value is null || value.Length < 3;
                public static bool Tiny(this string? value) => value is null || value.Length < 2;
            }
        }

        namespace Lib.Types
        {
            public sealed class Thing { public string? Name { get; set; } }
        }

        public static class Samples
        {
            public static Lib.Types.Thing Long() => new() { Name = "longer" };
        }
        """;

    [Fact]
    public void Plain_static_and_alias_usings_are_copied()
    {
        var result = GeneratorHarness.Run(Library, """
            using Makoto;
            using Lib.Text;
            using static Lib.Text.Checks;
            using Alias = Lib.Text.Checks;
            using Thing = Lib.Types.Thing;

            namespace Demo;

            [Validator]
            public sealed partial class V : Validator<Thing>
            {
                protected override void Configure()
                {
                    Property(x => x.Name)
                        .Require(x => x.Tiny())
                        .Require(x => Short(x))
                        .Require(x => Alias.Short(x));
                }
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Equal(["Name", "Name", "Name"], result.Load().Paths("Demo.V", "Long"));

        var code = result.Generated("Demo.V");
        Assert.Contains("using static Lib.Text.Checks;", code);
        Assert.Contains("using Alias = Lib.Text.Checks;", code);
    }

    [Fact]
    public void Usings_inside_a_namespace_stay_inside_it()
    {
        var result = GeneratorHarness.Run(Library, """
            namespace Demo
            {
                using Makoto;
                using Lib.Types;
                using Short = Lib.Text.Checks;

                [Validator]
                public sealed partial class V : Validator<Thing>
                {
                    protected override void Configure() => Property(x => x.Name).Require(x => Short.Short(x));
                }
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Single(result.Load().Failures("Demo.V", "Long"));
    }

    [Fact]
    public void Usings_after_a_file_scoped_namespace_are_copied()
    {
        var result = GeneratorHarness.Run(Library, """
            namespace Demo;

            using Makoto;
            using Lib.Text;
            using Lib.Types;

            [Validator]
            public sealed partial class V : Validator<Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Tiny());
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Single(result.Load().Failures("Demo.V", "Long"));
    }

    [Fact]
    public void Nested_namespace_blocks_keep_each_level_of_directives()
    {
        // Inner depends on the outer alias, so flattening the two levels into one would not compile.
        var result = GeneratorHarness.Run(Library, """
            using Makoto;

            namespace Outer
            {
                using Text = Lib.Text;

                namespace Inner.Deep
                {
                    using Checks = Text.Checks;

                    [Validator]
                    public sealed partial class V : Validator<Lib.Types.Thing>
                    {
                        protected override void Configure() => Property(x => x.Name).Require(x => Checks.Short(x));
                    }
                }
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Single(result.Load().Failures("Outer.Inner.Deep.V", "Long"));
    }

    [Fact]
    public void Extern_aliases_are_copied_ahead_of_the_usings()
    {
        var library = ValidatorHarness.CompileToReference("namespace Lib; public static class Limits { public const int Max = 3; }")
            .WithAliases(["Extra"]);

        var result = ValidatorHarness.Run([library], Library, """
            extern alias Extra;
            using Makoto;
            using Limits = Extra::Lib.Limits;

            [Validator]
            public sealed partial class V : Validator<Lib.Types.Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x!.Length < Limits.Max);
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains("extern alias Extra;", result.Generated("V"));
    }

    [Fact]
    public void Validators_work_in_the_global_namespace()
    {
        var result = GeneratorHarness.Run(Library, """
            using Makoto;
            using Lib.Types;

            [Validator]
            public sealed partial class V : Validator<Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => false);
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.DoesNotContain("namespace", result.Generated("V"));
        Assert.Single(result.Load().Failures("V", "Long"));
    }

    [Fact]
    public void Global_usings_are_not_copied_but_still_apply()
    {
        var result = GeneratorHarness.Run(Library, "global using Lib.Text;", """
            global using Lib.Types;
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Tiny());
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.DoesNotContain("global using", result.Generated("V"));
        Assert.Single(result.Load().Failures("V", "Long"));
    }

    [Fact]
    public void Usings_come_from_the_file_that_holds_Configure()
    {
        var result = GeneratorHarness.Run(
            Library,
            """
            using Makoto;
            using Lib.Types;
            namespace Demo;

            [Validator]
            public sealed partial class V : Validator<Thing>;
            """,
            """
            using Lib.Text;
            namespace Demo;

            public sealed partial class V
            {
                protected override void Configure() => Property(x => x.Name).Require(x => x.Tiny());
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains("using Lib.Text;", result.Generated("Demo.V"));
        Assert.Single(result.Load().Failures("Demo.V", "Long"));
    }

    [Fact]
    public void Validators_with_the_same_name_in_different_namespaces_both_generate()
    {
        const string Validator = """
            [Makoto.Validator]
            public sealed partial class V : Makoto.Validator<Lib.Types.Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => false);
            }
            """;

        var result = GeneratorHarness.Run(Library, $"namespace A;\n{Validator}", $"namespace B;\n{Validator}");

        ValidatorHarness.AssertClean(result);
        Assert.Contains("A.V.Validator.g.cs", result.Sources.Keys);
        Assert.Contains("B.V.Validator.g.cs", result.Sources.Keys);
    }

    [Fact]
    public void An_accessibility_modifier_on_the_declaration_is_kept()
    {
        var result = GeneratorHarness.Run(Library, """
            using Makoto;

            [Validator]
            internal partial class V : Validator<Lib.Types.Thing>
            {
                protected override void Configure() { }
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.Contains("sealed partial class V", result.Generated("V"));
        Assert.True(result.Load().GetType("V")!.IsNotPublic);
    }

    [Fact]
    public void A_keyword_as_class_name_keeps_its_escape()
    {
        var result = GeneratorHarness.Run(Library, """
            using Makoto;

            [Validator]
            public sealed partial class @class : Validator<Lib.Types.Thing>
            {
                protected override void Configure() { }
            }
            """);

        ValidatorHarness.AssertClean(result);
    }

    [Theory]
    [InlineData("Lib.Types.Thing?")]
    [InlineData("int?")]
    [InlineData("(int A, string B)")]
    [InlineData("System.IComparable")]
    [InlineData("string[]")]
    [InlineData("System.Collections.Generic.List<Lib.Types.Thing>")]
    public void Unusual_model_types_compile(string model)
    {
        var result = GeneratorHarness.Run(Library, $$"""
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<{{model}}>
            {
                protected override void Configure() { }
            }
            """);

        ValidatorHarness.AssertClean(result);
    }

    [Fact]
    public void The_attribute_can_be_named_through_an_alias()
    {
        var result = GeneratorHarness.Run(Library, """
            using Makoto;
            using Check = Makoto.ValidatorAttribute;

            [Check]
            public sealed partial class V : Validator<Lib.Types.Thing>
            {
                protected override void Configure() => Property(x => x.Name).Require(x => false);
            }
            """);

        ValidatorHarness.AssertClean(result);
    }
}
