using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common;

public abstract class BaseParser<TStartNode>(ITokenNodeStream tokenNodeStream)
    where TStartNode : GreenNode
{
    public abstract TStartNode? Parse();

    protected abstract HashSet<string> Keywords { get; }

    private readonly ITokenNodeStream tokenStream = tokenNodeStream;

    protected TokenNode? Expect(TokenType type)
    {
        var tok = tokenStream.PeekToken();
        if (tok.Type == type)
        {
            if (tok.Type == TokenType.Name && tok.RawString != null && Keywords.Contains(tok.RawString))
                return null;

            return tokenStream.GetAndAdvance();
        }

        return null;
    }

    protected TokenNode? Expect(string name)
    {
        var tok = tokenStream.PeekToken();
        if (tok.RawString != null && tok.RawString.SequenceEqual(name))
            return tokenStream.GetAndAdvance();

        return null;
    }

    protected bool Lookahead(Func<GreenNode?> func, bool positive)
    {
        int mark = Mark();
        bool isParsed = func() is not null;
        Reset(mark);
        return isParsed == positive;
    }

    protected int Mark() => tokenStream.Index;

    protected void Reset(int index) => tokenStream.Index = index;
}
