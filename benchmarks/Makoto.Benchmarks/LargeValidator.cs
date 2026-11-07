using Makoto;
using Makoto.Rules;

public sealed record LargeModel(string P0, string P1, string P2, string P3, string P4, string P5, string P6, string P7, string P8, string P9, string P10, string P11, string P12, string P13, string P14, string P15, string P16, string P17, string P18, string P19, string P20, string P21, string P22, string P23, string P24, string P25, string P26, string P27, string P28, string P29, string P30, string P31, string P32, string P33, string P34, string P35, string P36, string P37, string P38, string P39)
{
    public static readonly LargeModel Valid = new("value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value");
    public static readonly LargeModel Invalid = new("value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "value", "");
}

[Validator]
public sealed partial class LargeValidator : Validator<LargeModel>
{
    protected override void Configure()
    {
        Property(x => x.P0).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P1).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P2).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P3).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P4).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P5).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P6).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P7).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P8).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P9).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P10).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P11).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P12).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P13).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P14).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P15).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P16).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P17).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P18).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P19).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P20).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P21).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P22).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P23).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P24).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P25).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P26).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P27).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P28).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P29).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P30).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P31).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P32).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P33).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P34).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P35).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P36).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P37).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P38).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
        Property(x => x.P39).Require(x => x.NotEmpty()).Require(x => x.MaxLength(50));
    }
}
