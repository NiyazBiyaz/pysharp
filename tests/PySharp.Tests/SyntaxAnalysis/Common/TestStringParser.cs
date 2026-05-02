using PySharp.SyntaxAnalysis.Common;

namespace PySharp.Tests.SyntaxAnalysis.Common;

public class TestStringParser
{
    [Theory]
    [InlineData("'123'", "123")]
    [InlineData("'bau'", "bau")]
    [InlineData("'''baubau'''", "baubau")]
    [InlineData("\"\"\"bau\nbau\"\"\"", "bau\nbau")]
    public void TestUnescaped(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("b'fwmc'", true)]
    [InlineData("rb'bau'", true)]
    [InlineData("\"bau\"", false)]
    [InlineData("Br\"fwmc\"", true)]
    public void TestHasByte(string input, bool expected)
    {
        bool actual = StringParser.HasByte(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("'''bau\nbau'''", "bau\nbau")]
    [InlineData("'''bau\r\nbau'''", "bau\nbau")]
    [InlineData("'''bau\rbau'''", "bau\nbau")]
    [InlineData("r'''bau\nbau'''", "bau\nbau")] // CPython also normalize raw string line feeds.
    [InlineData("r'''bau\r\nbau'''", "bau\nbau")]
    [InlineData("r'''bau\rbau'''", "bau\nbau")]
    public void TestLineFeedNormalization(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"'bau\nbau'", "bau\nbau")]
    [InlineData(@"'bau\rbau'", "bau\rbau")]
    [InlineData(@"'bau\fbau'", "bau\fbau")]
    [InlineData(@"'bau\tbau'", "bau\tbau")]
    [InlineData(@"'bau\bbau'", "bau\bbau")]
    [InlineData(@"'bau\vbau'", "bau\vbau")]
    [InlineData(@"'bau\abau'", "bau\abau")]
    [InlineData(@"'bau\0bau'", "bau\0bau")]
    [InlineData(@"'bau\'bau'", "bau'bau")]
    [InlineData(@"'bau\""bau'", "bau\"bau")]
    [InlineData(@"'bau\\bau'", "bau\\bau")]
    public void TestEscapeSequences(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"'bau\xaabau'", "bau\x00aabau", Skip = "Not implemented yet.")]
    [InlineData(@"'bau\377bau'", "bau\x00ffbau", Skip = "Not implemented yet.")]
    [InlineData(@"'bau\100bau'", "bau\x0040bau", Skip = "Not implemented yet.")]
    [InlineData(@"'bau\uaaaabau'", "bau\uaaaabau", Skip = "Not implemented yet.")]
    [InlineData(@"'bau\U0001F680bau'", "bau\U0001F680bau", Skip = "Not implemented yet.")]
    public void TestNumericEscapeSequences(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"r'bau\nbau'", @"bau\nbau")]
    [InlineData(@"r'bau\rbau'", @"bau\rbau")]
    [InlineData(@"r'bau\fbau'", @"bau\fbau")]
    [InlineData(@"r'bau\tbau'", @"bau\tbau")]
    [InlineData(@"r'bau\bbau'", @"bau\bbau")]
    [InlineData(@"r'bau\vbau'", @"bau\vbau")]
    [InlineData(@"r'bau\abau'", @"bau\abau")]
    [InlineData(@"r'bau\0bau'", @"bau\0bau")]
    [InlineData(@"r'bau\'bau'", @"bau\'bau")]
    [InlineData(@"r'bau\""bau'", @"bau\""bau")]
    [InlineData(@"r'bau\\bau'", @"bau\\bau")]
    [InlineData(@"r'bau\xaabau'", @"bau\xaabau")]
    [InlineData(@"r'bau\377bau'", @"bau\377bau")]
    [InlineData(@"r'bau\100bau'", @"bau\100bau")]
    [InlineData(@"r'bau\uaaaabau'", @"bau\uaaaabau")]
    [InlineData(@"r'bau\U0001F680bau'", @"bau\U0001F680bau")]
    public void TestRawIsNotEscaped(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("'bau\\\nbau'", "baubau")]
    [InlineData("'bau\\\rbau'", "baubau")]
    [InlineData("'bau\\\r\nbau'", "baubau")]
    [InlineData("r'bau\\\nbau'", "baubau")]
    [InlineData("r'bau\\\rbau'", "baubau")]
    [InlineData("r'bau\\\r\nbau'", "baubau")]
    public void TestEscapedLineFeed(string input, string expected)
    {
        string actual = StringParser.ParseQuotedString(input);
        Assert.Equal(expected, actual);
    }
}
