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

    protected NodeArray<T>? Repeat<T>(Func<T?> ruleCall, int minCount)
        where T : GreenNode
    {
        T? node;
        List<T> nodes = [];
        while ((node = ruleCall()) is not null)
            nodes.Add(node);

        if (nodes.Count < minCount)
            return null;

        return new(nodes);
    }

    protected NodeArray<TokenNode>? Repeat(TokenType type, int minCount)
    {
        TokenNode? tok;
        List<TokenNode> nodes = [];
        while ((tok = Expect(type)) is not null)
            nodes.Add(tok);

        if (nodes.Count < minCount)
            return null;

        return new(nodes);
    }

    protected NodeArray<TokenNode>? Repeat(string str, int minCount)
    {
        TokenNode? tok;
        List<TokenNode> nodes = [];
        while ((tok = Expect(str)) is not null)
            nodes.Add(tok);

        if (nodes.Count < minCount)
            return null;

        return new(nodes);
    }

    protected bool Lookahead<T>(Func<T?> func, bool positive)
        where T : GreenNode
    {
        int mark = Mark();
        bool isParsed = func() is not null;
        Reset(mark);
        return isParsed == positive;
    }

    protected bool Lookahead(string str, bool positive)
    {
        int mark = Mark();
        bool isParsed = Expect(str) is not null;
        Reset(mark);
        return isParsed == positive;
    }

    protected bool Lookahead(TokenType type, bool positive)
    {
        int mark = Mark();
        bool isParsed = Expect(type) is not null;
        Reset(mark);
        return isParsed == positive;
    }

    protected NodeArray<T>? Gather<T>(Func<T?> ruleCall, Func<GreenNode?> sepCall)
        where T : GreenNode
    {
        var node = ruleCall();

        if (node is null)
            return null;

        List<T> gathered = [node];

        while (true)
        {
            int mark = Mark();
            if (sepCall() is null)
                break;

            node = ruleCall();
            if (node is null)
            {
                Reset(mark);
                break;
            }
            gathered.Add(node);
        }

        return [.. gathered];
    }

    protected NodeArray<T>? Gather<T>(Func<T?> ruleCall, TokenType sepTokenType)
        where T : GreenNode
    {
        var node = ruleCall();

        if (node is null)
            return null;

        List<T> gathered = [node];

        while (true)
        {
            int mark = Mark();
            if (Expect(sepTokenType) is null)
                break;

            node = ruleCall();
            if (node is null)
            {
                Reset(mark);
                break;
            }
            gathered.Add(node);
        }

        return [.. gathered];
    }

    protected NodeArray<T>? Gather<T>(Func<T?> ruleCall, string sepTokenLiteral)
        where T : GreenNode
    {
        var node = ruleCall();

        if (node is null)
            return null;

        List<T> gathered = [node];

        while (true)
        {
            int mark = Mark();
            if (Expect(sepTokenLiteral) is null)
                break;

            node = ruleCall();
            if (node is null)
            {
                Reset(mark);
                break;
            }
            gathered.Add(node);
        }

        return [.. gathered];
    }

    protected int Mark() => tokenStream.Index;

    protected void Reset(int index) => tokenStream.Index = index;
}
