using PySharp.SyntaxAnalysis.Common;

namespace PySharp.Tests.SyntaxAnalysis.Common;

public class TestTextPositionMap
{
    [Fact]
    public void TestGetLines_Easy()
    {
        const string lines = """
        012
        456
        890

        """;

        // Fourth char for the line feed.
        int[][] indexesOfLines = [
            [0, 1, 2, 3],
            [4, 5, 6, 7],
            [8, 9, 10, 11],
        ];

        var textMap = new TextPositionMap();
        foreach (int linefeed in lines.Index().Where(c => c.Item == '\n').Select(c => c.Index))
        {
            textMap.Append(linefeed);
        }

        for (int line = 0; line < indexesOfLines.Length; line++)
        {
            foreach (int charIndex in indexesOfLines[line])
            {
                Assert.Equal(line, textMap.GetLineForPosition(charIndex));
            }
        }
    }

    [Fact]
    public void TestGetLines_WithoutLastLinefeed()
    {
        const string lines = """
        012
        456
        890
        """;

        // Fourth char for the line feed.
        int[][] indexesOfLines = [
            [0, 1, 2, 3],
            [4, 5, 6, 7],
            [8, 9, 10],
        ];

        var textMap = new TextPositionMap();
        foreach (int linefeed in lines.Index().Where(c => c.Item == '\n').Select(c => c.Index))
        {
            textMap.Append(linefeed);
        }

        for (int line = 0; line < indexesOfLines.Length; line++)
        {
            foreach (int charIndex in indexesOfLines[line])
            {
                Assert.Equal(line, textMap.GetLineForPosition(charIndex));
            }
        }
    }

    [Fact]
    public void TestAppend_ShouldAlwaysBeSorted()
    {
        var textMap = new TextPositionMap();
        textMap.Append(1);
        textMap.Append(5);
        textMap.Append(10);
        Assert.Throws<ArgumentOutOfRangeException>(() => textMap.Append(7));
    }
}
