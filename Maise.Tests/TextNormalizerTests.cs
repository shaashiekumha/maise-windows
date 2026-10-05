using Maise.Core.Text;
using Xunit;

namespace Maise.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Dr. Smith visited Mr. Jones.", "doctor Smith visited mister Jones.")]
    [InlineData("Prof. Davis and Sgt. Miller, etc.", "professor Davis and sergeant Miller, et cetera")]
    [InlineData("e.g. apple, i.e. fruit", "for example apple, that is fruit")]
    public void NormalizesAbbreviations(string input, string expected)
    {
        var actual = TextNormalizer.NormalizeText(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("42", "forty two")]
    [InlineData("100", "one hundred")]
    [InlineData("125", "one hundred and twenty five")]
    [InlineData("1,000", "one thousand")]
    [InlineData("3.14", "three point one four")]
    [InlineData("-5", "minus five")]
    public void NormalizesNumbers(string input, string expected)
    {
        var actual = TextNormalizer.NormalizeText(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("1st", "first")]
    [InlineData("2nd", "second")]
    [InlineData("3rd", "third")]
    [InlineData("21st", "twenty first")]
    [InlineData("100th", "one hundredth")]
    public void NormalizesOrdinals(string input, string expected)
    {
        var actual = TextNormalizer.NormalizeText(input);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SplitsSentencesCorrectly()
    {
        var text = "Hello world! How are you doing? I am fine. This is great.";
        var sentences = TextNormalizer.SplitSentences(text);
        Assert.Equal(4, sentences.Count);
        Assert.Equal("Hello world!", sentences[0]);
        Assert.Equal("How are you doing?", sentences[1]);
        Assert.Equal("I am fine.", sentences[2]);
        Assert.Equal("This is great.", sentences[3]);
    }
}
