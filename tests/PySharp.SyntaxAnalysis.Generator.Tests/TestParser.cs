using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator.Tests;

// I wanted to put this somewhere suitable, but the only working parser for testing Views is here.
// Anyway PegenNet might come more independent project and test it also matters.
public class TestParser
{
    [Fact]
    public void TestViewPositioning_Lines()
    {
        const string src = """
        @header "using BauBau.Mindset;"
        @parser_name "PonDeRingParser"

        @bau
        BauBau: "Fluffy" "Fuzzy"
        """;
        var view = getView(src);

        Assert.Equal(0, view.Metadata[0].Position.Line);
        Assert.Equal(1, view.Metadata[1].Position.Line);

        Assert.Equal(3, view.Rules[0].Position.Line);
        Assert.Equal(4, view.Rules[0].EndPosition.Line);
    }

    private static GrammarView getView(string src)
    {
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(new StringBuffer(src + '\n')), true);
        var tokenStream = new TokenNodeStream(tokenizer);
        var parser = new GrammarParser(tokenStream);
        var grammar = parser.Parse();
        Debug.Assert(grammar != null, "Invalid test code");
        return grammar.GetView(TokenPosition.StartOfFile, null);
    }
}
