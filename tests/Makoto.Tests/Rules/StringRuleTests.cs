namespace Makoto.Tests.Rules;

using Makoto.Rules;

public class StringRuleTests
{
    [Theory]
    [InlineData("a", true)]
    [InlineData(" ", true)]
    [InlineData("\0", true)]
    [InlineData("é", true)]
    [InlineData("😀", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NotEmpty(string? value, bool expected) => Assert.Equal(expected, value.NotEmpty());

    [Theory]
    [InlineData("a", true)]
    [InlineData(" a ", true)]
    [InlineData("😀", true)]
    [InlineData("​", true)] // Zero-width space is a format character, not white space.
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData(" ", false)]
    [InlineData("\t\r\n", false)]
    [InlineData(" ", false)]
    [InlineData(" ", false)]
    [InlineData("　", false)]
    public void NotBlank(string? value, bool expected) => Assert.Equal(expected, value.NotBlank());

    [Theory]
    [InlineData("abc", 2, true)]
    [InlineData("abc", 3, true)]
    [InlineData("abc", 4, false)]
    [InlineData("", 0, true)]
    [InlineData("", 1, false)]
    [InlineData("", -1, true)]
    [InlineData(null, 0, false)]
    [InlineData("😀", 2, true)] // Lengths are UTF-16 code units.
    [InlineData("😀", 3, false)]
    [InlineData("é", 2, true)]
    public void MinLength(string? value, int min, bool expected) => Assert.Equal(expected, value.MinLength(min));

    [Theory]
    [InlineData("abc", 4, true)]
    [InlineData("abc", 3, true)]
    [InlineData("abc", 2, false)]
    [InlineData("", 0, true)]
    [InlineData("", -1, false)]
    [InlineData(null, 10, false)]
    [InlineData("😀", 2, true)]
    [InlineData("😀", 1, false)]
    [InlineData("abc", int.MaxValue, true)]
    public void MaxLength(string? value, int max, bool expected) => Assert.Equal(expected, value.MaxLength(max));

    [Theory]
    [InlineData("ab", 2, 4, true)]
    [InlineData("abc", 2, 4, true)]
    [InlineData("abcd", 2, 4, true)]
    [InlineData("a", 2, 4, false)]
    [InlineData("abcde", 2, 4, false)]
    [InlineData("abc", 3, 3, true)]
    [InlineData("abc", 4, 2, false)]
    [InlineData("", 0, 0, true)]
    [InlineData(null, 0, 10, false)]
    [InlineData("😀", 2, 2, true)]
    public void LengthBetween(string? value, int min, int max, bool expected) =>
        Assert.Equal(expected, value.LengthBetween(min, max));

    [Theory]
    [InlineData("abc", "a", true)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abcd", false)]
    [InlineData("abc", "b", false)]
    [InlineData("abc", "", true)]
    [InlineData("", "", true)]
    [InlineData("", "a", false)]
    [InlineData(null, "a", false)]
    [InlineData(null, "", false)]
    [InlineData("ABC", "a", false)]
    [InlineData("😀a", "😀", true)]
    [InlineData("é", "e", true)] // Ordinal: a culture compare sees a different letter here.
    [InlineData("é", "é", false)] // Ordinal: a culture compare sees the same letter here.
    [InlineData("­abc", "a", false)] // Ordinal: a culture compare ignores the soft hyphen.
    public void StartsWithText(string? value, string prefix, bool expected) =>
        Assert.Equal(expected, value.StartsWithText(prefix));

    [Theory]
    [InlineData("abc", "c", true)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "zabc", false)]
    [InlineData("abc", "b", false)]
    [InlineData("abc", "", true)]
    [InlineData("", "", true)]
    [InlineData("", "a", false)]
    [InlineData(null, "a", false)]
    [InlineData(null, "", false)]
    [InlineData("ABC", "c", false)]
    [InlineData("a😀", "😀", true)]
    [InlineData("é", "́", true)]
    [InlineData("é", "é", false)]
    [InlineData("abc­", "c", false)]
    public void EndsWithText(string? value, string suffix, bool expected) =>
        Assert.Equal(expected, value.EndsWithText(suffix));

    [Theory]
    [InlineData("abc", "b", true)]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abcd", false)]
    [InlineData("abc", "", true)]
    [InlineData("", "", true)]
    [InlineData("", "a", false)]
    [InlineData(null, "a", false)]
    [InlineData(null, "", false)]
    [InlineData("abc", "B", false)]
    [InlineData("日本語", "本", true)]
    [InlineData("a😀b", "😀", true)]
    [InlineData("á", "́", true)]
    [InlineData("straße", "STRASSE", false)]
    public void ContainsText(string? value, string text, bool expected) =>
        Assert.Equal(expected, value.ContainsText(text));
}
