namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class FormatRuleTests
{
    [Theory]
    [InlineData("a@b", true)]
    [InlineData("first.last+tag@example.co.uk", true)]
    [InlineData("ü@ñ.de", true)]
    [InlineData("😀@😀", true)]
    [InlineData("a b@c d", true)] // Deliberately loose: one @ with something on both sides is the whole check.
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("ab", false)]
    [InlineData("@", false)]
    [InlineData("@@", false)]
    [InlineData("@b", false)]
    [InlineData("a@", false)]
    [InlineData("a@@b", false)]
    [InlineData("a@b@c", false)]
    [InlineData("@a@", false)]
    public void IsEmail(string? value, bool expected) => Assert.Equal(expected, value.IsEmail());

    [Theory]
    [InlineData("http://a.b", true)]
    [InlineData("https://a.b", true)]
    [InlineData("HTTP://A.B", true)]
    [InlineData("HtTpS://a.b", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("https://example.com/a/b?c=d#e", true)]
    [InlineData("http://127.0.0.1", true)]
    [InlineData("http://[::1]/", true)]
    [InlineData("http://münchen.de", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("example.com", false)]
    [InlineData("/relative/path", false)] // Uri.TryCreate reads this as a file URI on Unix.
    [InlineData("ftp://a.b", false)]
    [InlineData("file:///a", false)]
    [InlineData("mailto:a@b.c", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("http", false)]
    [InlineData("http://", false)]
    [InlineData("https://", false)]
    [InlineData("http:/a.b", false)]
    [InlineData("http:a.b", false)]
    [InlineData("httpx://a.b", false)]
    [InlineData("http://exa mple.com", false)]
    [InlineData(" http://a.b", false)]
    [InlineData("http://a.b/a b", false)]
    [InlineData("http://a.b/a\tb", false)]
    [InlineData("http://a.b/\n", false)]
    [InlineData("http://a.b/a\u007Fb", false)]
    [InlineData("http://a.b/a%20b", true)]
    public void IsUrl(string? value, bool expected) => Assert.Equal(expected, value.IsUrl());

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e", true)]
    [InlineData("0F8FAD5B-D9CB-469F-A165-70867728950E", true)]
    [InlineData("0f8fad5bd9cb469fa16570867728950e", true)]
    [InlineData("{0f8fad5b-d9cb-469f-a165-70867728950e}", true)]
    [InlineData("(0f8fad5b-d9cb-469f-a165-70867728950e)", true)]
    [InlineData("{0x0f8fad5b,0xd9cb,0x469f,{0xa1,0x65,0x70,0x86,0x77,0x28,0x95,0x0e}}", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950", false)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950e0", false)]
    [InlineData("0f8fad5b-d9cb-469f-a165-70867728950g", false)]
    [InlineData("0f8fad5b_d9cb_469f_a165_70867728950e", false)]
    public void IsGuid(string? value, bool expected) => Assert.Equal(expected, value.IsGuid());

    [Theory]
    [InlineData("0", true)]
    [InlineData("0123456789", true)]
    [InlineData("007", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(" 1", false)]
    [InlineData("1 ", false)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("/", false)] // Just below '0'.
    [InlineData(":", false)] // Just above '9'.
    [InlineData("٣", false)] // Arabic-Indic digit: char.IsDigit says yes, this rule is ASCII only.
    [InlineData("１", false)] // Fullwidth digit.
    public void IsDigits(string? value, bool expected) => Assert.Equal(expected, value.IsDigits());

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("\0", true)]
    [InlineData("\u007F", true)]
    [InlineData(null, false)]
    [InlineData("\u0080", false)]
    [InlineData("é", false)]
    [InlineData("abcÿ", false)]
    [InlineData("😀", false)]
    public void IsAscii(string? value, bool expected) => Assert.Equal(expected, value.IsAscii());

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(1000)]
    public void Vectorized_checks_find_a_bad_char_at_any_length(int length)
    {
        var digits = new string('1', length);
        var ascii = new string('a', length);

        Assert.Equal(length != 0, digits.IsDigits());
        Assert.False((digits + "x").IsDigits());
        Assert.False(("x" + digits).IsDigits());

        Assert.True(ascii.IsAscii());
        Assert.False((ascii + "é").IsAscii());
        Assert.False(("é" + ascii).IsAscii());
    }
}
