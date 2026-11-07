using Makoto;
using Makoto.Rules;
using BenchmarkDotNet.Attributes;

public sealed record Line(string Sku, int Quantity);

public sealed record Order(string Id, List<Line> Lines);

[Validator]
public sealed partial class LineValidator : Validator<Line>
{
    protected override void Configure()
    {
        Property(x => x.Sku).Require(x => x.NotEmpty());
        Property(x => x.Quantity).Require(x => x.Between(1, 99));
    }
}

[Validator]
public sealed partial class OrderValidator : Validator<Order>
{
    protected override void Configure()
    {
        Property(x => x.Id).Require(x => x.NotEmpty());
        Each(x => x.Lines).ValidateWith<LineValidator>();
    }
}

/// <summary>Shapes the small validator doesn't show: wide validators, and collections of nested validators.</summary>
[MemoryDiagnoser]
public class ScaleBenchmarks
{
    private static readonly Order ValidOrder = new("o", Enumerable.Range(0, 100).Select(i => new Line("sku", 5)).ToList());
    private static readonly Order TenPercentInvalid = new("o", Enumerable.Range(0, 100).Select(i => new Line(i % 10 == 0 ? "" : "sku", 5)).ToList());

    private readonly OrderValidator _order = new();
    private readonly LargeValidator _large = new();

    [Benchmark]
    public bool Order_valid() => _order.Validate(ValidOrder).IsValid;

    [Benchmark]
    public bool Order_valid_static() => OrderValidator.Check(ValidOrder);

    [Benchmark]
    public bool Order_10pct_invalid() => _order.Validate(TenPercentInvalid).IsValid;

    [Benchmark]
    public bool Large_valid() => _large.Validate(LargeModel.Valid).IsValid;

    [Benchmark]
    public bool Large_valid_static() => LargeValidator.Check(LargeModel.Valid);

    [Benchmark]
    public bool Large_invalid_last() => _large.Validate(LargeModel.Invalid).IsValid;
}
