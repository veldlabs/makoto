namespace Makoto.Generators.Tests.Validators;

/// <summary>ValidateWith, Each and value-typed models.</summary>
public class NestingBehaviorTests
{
    private const string Models = """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Linq;
        using Makoto;

        public sealed class Address { public string? City { get; set; } public Address? Parent { get; set; } }
        public sealed class Item { public string? Name { get; set; } public int Quantity { get; set; } }
        public record struct Point(int X, int Y);
        public readonly struct Money { public decimal Amount { get; init; } }

        public sealed class Order
        {
            public Address? Address { get; set; }
            public Address Billing { get; set; } = new() { City = "Oslo" };
            public string[]? Tags { get; set; }
            public List<string>? Notes { get; set; }
            public IEnumerable<string>? Lazy { get; set; }
            public IReadOnlyList<Item>? Lines { get; set; }
            public List<Item>? Items { get; set; }
            public Item[]? Array { get; set; }
            public Point Origin { get; set; }
            public Point? Target { get; set; }
            public List<Point>? Points { get; set; }
            public Point?[]? Maybe { get; set; }
            public ImmutableArray<string> Frozen { get; set; } = [];
            public ImmutableArray<string>? MaybeFrozen { get; set; }
            public HashSet<int>? Numbers { get; set; }
            public Dictionary<string, int>? Stock { get; set; }
            public string? Text { get; set; }
            public object[]? Covariant { get; set; }
            public Order? Inner { get; set; }
        }

        [ValidationRule(Message = "'{PropertyName}' is blank.")]
        public sealed class FilledRule : ValidationRule<string>
        {
            public override bool Evaluate(string value) => value.Length != 0;
        }

        [Validator]
        public sealed partial class AddressValidator : Validator<Address>
        {
            protected override void Configure()
            {
                Property(x => x.City).Require(x => x.Filled());
                Property(x => x.Parent).ValidateWith<AddressValidator>();
            }
        }

        [Validator]
        public sealed partial class ItemValidator : Validator<Item>
        {
            protected override void Configure()
            {
                Property(x => x.Name).Require(x => x.Filled());
                Property(x => x.Quantity).Require(x => x > 0).WithCode("Quantity");
            }
        }

        [Validator]
        public sealed partial class PointValidator : Validator<Point>
        {
            protected override void Configure()
            {
                Property(x => x.X).Require(x => x >= 0);
                Property(x => x.Y).Require((model, y) => y >= model.X);
            }
        }

        public static class Samples
        {
            public static Order Empty() => new();
            public static Order NoCity() => new() { Address = new() };
            public static Order DeepNoCity() => new() { Address = new() { City = "a", Parent = new() { City = "b", Parent = new() } } };
            public static Order BadBilling() => new() { Billing = new() };
            public static Order BadOrigin() => new() { Origin = new(-1, -2) };
            public static Order BadTarget() => new() { Target = new(-1, 0) };
            public static Order GoodTarget() => new() { Target = new(1, 2) };
            public static Order Tagged() => new() { Tags = ["a", "", "c", ""] };
            public static Order Noted() => new() { Notes = ["", "b", ""] };
            public static Order Lazily() => new() { Lazy = Enumerable.Repeat("", 2) };
            public static Order Items() => new()
            {
                Items = [new() { Name = "a", Quantity = 1 }, new() { Name = "", Quantity = 0 }, null!, new() { Quantity = 1 }],
            };
            public static Order Array() => new() { Array = [new() { Name = "a", Quantity = 1 }, null!, new() { Name = "", Quantity = 1 }] };
            public static Order Lines() => new() { Lines = [new() { Name = "", Quantity = 1 }] };
            public static Order Points() => new() { Points = [new(1, 2), new(-1, 3)] };
            public static Order Maybe() => new() { Maybe = [null, new(-1, 0)] };
            public static Order Frozen() => new() { Frozen = ["", "a"] };
            public static Order MaybeFrozen() => new() { MaybeFrozen = ["a", ""] };
            public static Order DefaultFrozen() => new() { Frozen = default, MaybeFrozen = default(ImmutableArray<string>) };
            public static Order Numbers() => new() { Numbers = [1, -1] };
            public static Order Stock() => new() { Stock = new() { ["a"] = -1 } };
            public static Order Text() => new() { Text = "a b" };
            public static Order Covariant() => new() { Covariant = new string[] { "a", "" } };
            public static Order Recursive() => new() { Inner = new() { Inner = new() { Items = [new() { Name = "", Quantity = 1 }] } } };
        }
        """;

    private static System.Reflection.Assembly Validator(string configure) =>
        ValidatorHarness.Load(Models, $$"""
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Order>
            {
                protected override void Configure()
                {
                    {{configure}}
                }
            }
            """);

    [Fact]
    public void ValidateWith_prefixes_nested_paths()
    {
        var assembly = Validator("Property(x => x.Address).ValidateWith<AddressValidator>();");

        Assert.Equal(["Address.City|Filled|'City' is blank."], assembly.Failures("V", "NoCity"));
        Assert.Equal(["Address.Parent.Parent.City"], assembly.Paths("V", "DeepNoCity"));
    }

    [Fact]
    public void ValidateWith_skips_null()
    {
        Assert.Empty(Validator("Property(x => x.Address).ValidateWith<AddressValidator>();").Failures("V", "Empty"));
    }

    [Fact]
    public void ValidateWith_uses_Named_as_the_prefix()
    {
        var assembly = Validator("""Property(x => x.Billing).ValidateWith<AddressValidator>().Named("Invoice");""");

        Assert.Equal(["Invoice.City"], assembly.Paths("V", "BadBilling"));
    }

    [Fact]
    public void An_empty_name_adds_no_prefix()
    {
        var assembly = Validator("""Property(x => x.Billing).ValidateWith<AddressValidator>().Named("");""");

        Assert.Equal(["City"], assembly.Paths("V", "BadBilling"));
    }

    [Fact]
    public void ValidateWith_works_alongside_requires()
    {
        var assembly = Validator("""
            Property(x => x.Address)
                .Require(x => x is null).WithCode("First")
                .ValidateWith<AddressValidator>()
                .Require(x => x is null).WithCode("Last");
            """);

        Assert.Equal(["Address", "Address.City", "Address"], assembly.Paths("V", "NoCity"));
    }

    [Fact]
    public void StopOnFailure_stops_before_and_after_ValidateWith()
    {
        var before = Validator("""
            Property(x => x.Address).StopOnFailure()
                .Require(x => x is null)
                .ValidateWith<AddressValidator>();
            """);

        var after = Validator("""
            Property(x => x.Address).StopOnFailure()
                .ValidateWith<AddressValidator>()
                .Require(x => x is null);
            """);

        Assert.Equal(["Address"], before.Paths("V", "NoCity"));
        Assert.Equal(["Address.City"], after.Paths("V", "NoCity"));
    }

    [Fact]
    public void Two_ValidateWith_in_one_chain_compile()
    {
        var assembly = Validator("""Property(x => x.Address).ValidateWith<AddressValidator>().ValidateWith<AddressValidator>().StopOnFailure();""");

        Assert.Equal(["Address.City"], assembly.Paths("V", "NoCity"));
    }

    [Fact]
    public void Validators_can_recurse()
    {
        var assembly = ValidatorHarness.Load(Models, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Order>
            {
                protected override void Configure()
                {
                    Property(x => x.Inner).ValidateWith<V>();
                    Each(x => x.Items).ValidateWith<ItemValidator>();
                }
            }
            """);

        Assert.Equal(["Inner.Inner.Items[0].Name"], assembly.Paths("V", "Recursive"));
    }

    [Fact]
    public void ValidateWith_on_a_struct_has_no_null_check()
    {
        var result = GeneratorHarness.Run(Models, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Order>
            {
                protected override void Configure() => Property(x => x.Origin).ValidateWith<PointValidator>();
            }
            """);

        ValidatorHarness.AssertClean(result);
        Assert.DoesNotContain("is not null", result.Generated("V"));
        Assert.Equal(["Origin.X", "Origin.Y"], result.Load().Paths("V", "BadOrigin"));
    }

    [Fact]
    public void ValidateWith_on_a_nullable_struct_skips_null_and_unwraps()
    {
        var assembly = Validator("Property(x => x.Target).ValidateWith<PointValidator>();");

        Assert.Empty(assembly.Failures("V", "Empty"));
        Assert.Empty(assembly.Failures("V", "GoodTarget"));
        Assert.Equal(["Target.X"], assembly.Paths("V", "BadTarget"));
    }

    [Fact]
    public void Struct_models_have_no_null_guard()
    {
        var result = GeneratorHarness.Run(Models);
        ValidatorHarness.AssertClean(result);

        Assert.DoesNotContain("ThrowIfNull", result.Generated("PointValidator"));
        Assert.Contains("ThrowIfNull", result.Generated("ItemValidator"));
    }

    [Fact]
    public void Each_reports_indexes_for_arrays()
    {
        var assembly = Validator("Each(x => x.Tags).Require(x => x.Filled());");

        Assert.Equal(["Tags[1]|Filled|'Tags' is blank.", "Tags[3]|Filled|'Tags' is blank."], assembly.Failures("V", "Tagged"));
    }

    [Fact]
    public void Each_reports_indexes_for_lists()
    {
        Assert.Equal(["Notes[0]", "Notes[2]"], Validator("Each(x => x.Notes).Require(x => x.Filled());").Paths("V", "Noted"));
    }

    [Fact]
    public void Each_reports_indexes_for_other_enumerables()
    {
        Assert.Equal(["Lazy[0]", "Lazy[1]"], Validator("Each(x => x.Lazy).Require(x => x.Filled());").Paths("V", "Lazily"));
    }

    [Fact]
    public void Each_picks_the_loop_by_collection_type()
    {
        var result = GeneratorHarness.Run(Models, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Order>
            {
                protected override void Configure()
                {
                    Each(x => x.Tags).Require(x => x.Filled());
                    Each(x => x.Notes).Require(x => x.Filled());
                    Each(x => x.Lazy).Require(x => x.Filled());
                    Each(x => x.Lines).ValidateWith<ItemValidator>();
                }
            }
            """);

        ValidatorHarness.AssertClean(result);
        var code = result.Generated("V");
        Assert.Contains("var span0 = new global::System.ReadOnlySpan<string>(items0);", code);
        Assert.Contains("var span1 = global::System.Runtime.InteropServices.CollectionsMarshal.AsSpan(items1);", code);
        Assert.Contains("foreach (var item2 in items2)", code);
        Assert.Contains("for (var index3 = 0; index3 < items3.Count; index3++)", code);
        Assert.DoesNotContain("foreach (var item0", code);
        Assert.DoesNotContain("foreach (var item1", code);
        Assert.DoesNotContain("foreach (var item3", code);
    }

    [Theory]
    [InlineData("Each(x => x.Tags).Require(x => false);")]
    [InlineData("Each(x => x.Notes).Require(x => false);")]
    [InlineData("Each(x => x.Lazy).Require(x => false);")]
    [InlineData("Each(x => x.Items).ValidateWith<ItemValidator>();")]
    public void Each_skips_null_collections(string configure)
    {
        Assert.Empty(Validator(configure).Failures("V", "Empty"));
    }

    [Fact]
    public void Each_with_ValidateWith_reports_full_paths_and_skips_null_elements()
    {
        var assembly = Validator("Each(x => x.Items).ValidateWith<ItemValidator>();");

        Assert.Equal(["Items[1].Name", "Items[1].Quantity", "Items[3].Name"], assembly.Paths("V", "Items"));
    }

    [Fact]
    public void Each_with_ValidateWith_works_on_arrays_and_interfaces()
    {
        Assert.Equal(["Array[2].Name"], Validator("Each(x => x.Array).ValidateWith<ItemValidator>();").Paths("V", "Array"));
        Assert.Equal(["Lines[0].Name"], Validator("Each(x => x.Lines).ValidateWith<ItemValidator>();").Paths("V", "Lines"));
    }

    [Fact]
    public void Each_Named_changes_the_prefix()
    {
        var assembly = Validator("""Each(x => x.Tags).Require(x => x.Filled()).Named("Labels");""");

        Assert.Equal(
            ["Labels[1]|Filled|'Labels' is blank.", "Labels[3]|Filled|'Labels' is blank."],
            assembly.Failures("V", "Tagged"));
    }

    [Fact]
    public void Each_StopOnFailure_is_per_element()
    {
        var assembly = Validator("""
            Each(x => x.Tags).StopOnFailure()
                .Require(x => x.Filled()).WithCode("A")
                .Require(x => x.Filled()).WithCode("B");
            """);

        Assert.Equal(["Tags[1]|A|'Tags' is blank.", "Tags[3]|A|'Tags' is blank."], assembly.Failures("V", "Tagged"));
    }

    [Fact]
    public void Each_StopOnFailure_covers_ValidateWith()
    {
        var assembly = Validator("""
            Each(x => x.Items).StopOnFailure()
                .Require(x => x is null || x.Quantity != 0).WithCode("A")
                .ValidateWith<ItemValidator>();
            """);

        Assert.Equal(["Items[1]", "Items[3].Name"], assembly.Paths("V", "Items"));
    }

    [Fact]
    public void Each_when_and_model_lambdas_get_the_element_and_the_root()
    {
        var assembly = Validator("""
            Each(x => x.Tags).Require((model, x) => model.Tags!.Length == 0 && x.Filled()).When(x => x == "c");
            """);

        Assert.Equal(["Tags[2]|Require|'Tags' is not valid."], assembly.Failures("V", "Tagged"));
    }

    [Fact]
    public void Each_paths_can_go_through_members()
    {
        var assembly = Validator("Each(x => x.Inner!.Inner?.Items).Require(x => false);");

        Assert.Equal(["Inner.Inner.Items[0]"], assembly.Paths("V", "Recursive"));
    }

    [Fact]
    public void Each_over_struct_elements_has_no_null_check()
    {
        var assembly = Validator("Each(x => x.Points).ValidateWith<PointValidator>();");

        Assert.Equal(["Points[1].X"], assembly.Paths("V", "Points"));
    }

    [Fact]
    public void Each_over_nullable_struct_elements_skips_nulls()
    {
        var assembly = Validator("Each(x => x.Maybe).ValidateWith<PointValidator>();");

        Assert.Equal(["Maybe[1].X"], assembly.Paths("V", "Maybe"));
    }

    [Fact]
    public void Each_over_a_struct_collection_has_no_null_check()
    {
        var assembly = Validator("Each(x => x.Frozen).Require(x => x.Filled());");

        Assert.Equal(["Frozen[0]"], assembly.Paths("V", "Frozen"));
    }

    [Fact]
    public void Each_over_a_nullable_struct_collection_skips_null_and_unwraps()
    {
        var assembly = Validator("Each<string>(x => x.MaybeFrozen).Require(x => x.Filled());");

        Assert.Empty(assembly.Failures("V", "Empty"));
        Assert.Equal(["MaybeFrozen[1]"], assembly.Paths("V", "MaybeFrozen"));
    }

    /// <summary>A deserializer leaves an omitted ImmutableArray property default, and foreach over that throws.</summary>
    [Fact]
    public void Each_over_a_default_ImmutableArray_validates_as_empty()
    {
        var result = GeneratorHarness.Run(Models, """
            using Makoto;

            [Validator]
            public sealed partial class V : Validator<Order>
            {
                protected override void Configure()
                {
                    Each(x => x.Frozen).Require(x => x.Filled());
                    Each<string>(x => x.MaybeFrozen).Require(x => x.Filled());
                }
            }
            """);

        ValidatorHarness.AssertClean(result);
        var code = result.Generated("V");
        Assert.Contains("if (!items0.IsDefault)", code);
        Assert.Contains("if (items1 is not null && !items1.GetValueOrDefault().IsDefault)", code);

        // Failures holds Validate, IsValid and Check to the same verdict, so all three are exercised.
        var assembly = result.Load();
        Assert.Empty(assembly.Failures("V", "DefaultFrozen"));
        Assert.Equal(["Frozen[0]", "MaybeFrozen[1]"], assembly.Paths("V", "Frozen").Concat(assembly.Paths("V", "MaybeFrozen")));
    }

    [Fact]
    public void Each_over_a_set_a_dictionary_and_a_string_uses_foreach()
    {
        var assembly = Validator("""
            Each(x => x.Numbers).Require(x => x > 0);
            Each(x => x.Stock).Require(x => x.Value > 0);
            Each(x => x.Text).Require(x => x != ' ');
            """);

        Assert.Equal(["Numbers[1]"], assembly.Paths("V", "Numbers"));
        Assert.Equal(["Stock[0]"], assembly.Paths("V", "Stock"));
        Assert.Equal(["Text[1]"], assembly.Paths("V", "Text"));
    }

    [Fact]
    public void Each_over_a_covariant_array_does_not_throw()
    {
        var assembly = Validator("""Each(x => x.Covariant).Require(x => x is not "");""");

        Assert.Equal(["Covariant[1]"], assembly.Paths("V", "Covariant"));
    }

    [Fact]
    public void Each_element_type_can_be_widened()
    {
        var assembly = Validator("""
            Each<object>(x => x.Tags).Require(x => x is not "");
            Each<object>(x => x.Notes).Require(x => x is not "");
            """);

        Assert.Equal(["Tags[1]", "Tags[3]"], assembly.Paths("V", "Tagged"));
        Assert.Equal(["Notes[0]", "Notes[2]"], assembly.Paths("V", "Noted"));
    }

    [Fact]
    public void Several_each_chains_do_not_clash()
    {
        var assembly = Validator("""
            Each(x => x.Tags).Require(x => x.Filled());
            Each(x => x.Tags).Require(x => x.Filled()).StopOnFailure();
            Each(x => x.Lazy).Require(x => x.Filled());
            Each(x => x.Lazy).Require(x => x.Filled());
            """);

        Assert.Equal(["Tags[1]", "Tags[3]", "Tags[1]", "Tags[3]"], assembly.Paths("V", "Tagged"));
    }

    [Fact]
    public void Validators_can_validate_collections_of_a_struct_model()
    {
        var assembly = ValidatorHarness.Load(Models, """
            using Makoto;

            public record struct Bag(int[] Values, Point Point);

            [Validator]
            public sealed partial class BagValidator : Validator<Bag>
            {
                protected override void Configure()
                {
                    Each(x => x.Values).Require((model, x) => model.Point.X < x);
                    Property(x => x.Point).ValidateWith<PointValidator>();
                }
            }

            public static class More { public static Bag Sample() => new([5, 0], new(1, 0)); }
            """);

        var type = assembly.GetType("BagValidator")!;
        var sample = assembly.GetType("More")!.GetMethod("Sample")!.Invoke(null, null);
        var result = (ValidationResult)type.GetMethod("Validate")!.Invoke(Activator.CreateInstance(type), [sample])!;

        Assert.Equal(["Values[1]", "Point.Y"], result.Failures.ToArray().Select(f => f.PropertyName));
        Assert.Equal(false, type.GetMethod("Check")!.Invoke(null, [sample]));
    }
}
