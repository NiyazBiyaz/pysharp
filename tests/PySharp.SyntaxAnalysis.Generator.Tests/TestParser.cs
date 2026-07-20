using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator.Tests;

// I wanted to put this somewhere suitable, but the only working parser for testing Views is here.
// Anyway PegenNet might come more independent project and test it also matters.
public class TestParser
{
    [Fact]
    public void TestViewPositioning_PlainPositions()
    {
        const string src = """
        @header "using BauBau.Mindset;"
        @parser_name "PonDeRingParser"

        """;
        var view = getView(src);

        Assert.Equal(00, view.Metadata[0].Position);
        Assert.Equal(32, view.Metadata[0].EndPosition);
        Assert.Equal(32, view.Metadata[0].EndPosition - view.Metadata[0].Position);
        Assert.Equal(32, view.Metadata[1].Position);
        Assert.Equal(63, view.Metadata[1].EndPosition);
        Assert.Equal(31, view.Metadata[1].EndPosition - view.Metadata[1].Position);
    }

    private static GrammarView getView(string src)
    {
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(new StringBuffer(src + '\n')), true);
        var tokenStream = new TokenNodeStream(tokenizer);
        var parser = new GrammarParser(tokenStream);
        var grammar = parser.Parse();
        Debug.Assert(grammar != null, "Invalid test code");
        return grammar.GetView(0, null);
    }
}
