using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common;

public abstract class BaseParser<TStartNode>(ITokenNodeStream tokenNodeStream)
    where TStartNode : IGreenNode
{
    public abstract TStartNode? Parse();

    protected abstract HashSet<string> Keywords { get; }

    private readonly ITokenNodeStream tokenStream = tokenNodeStream;

    protected static IMemoContainer<TNode> CreateContainer<TNode>()
        where TNode : IGreenNode => new DictMemoContainer<TNode>();

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

    protected int Mark() => tokenStream.Index;

    protected void Reset(int index) => tokenStream.Index = index;

    private const string parser_verbose = "PARSER_VERBOSE";
    private const string log_indent_string = "  ";

    [Conditional(parser_verbose)]
    protected void LogRuleEntered(string ruleName) => logIndent($"Entering rule `{ruleName}`");

    [Conditional(parser_verbose)]
    protected void LogRuleFailed(string ruleName) => logIndent($"Rule `{ruleName}` is failed (null will be returned)");

    [Conditional(parser_verbose)]
    protected void LogRuleMemoUsed<T>(string ruleName, int position, MemoEntry<T> memo)
        where T : IGreenNode
    {
        string nullInfo = memo.Cache == null ? "null" : "not null";
        logIndent($"Rule `{ruleName}` used memoized cache for position {position} ({nullInfo}); memo end position is {memo.EndPosition}");
    }

    [Conditional(parser_verbose)]
    protected void LogRuleMemoCreated(string ruleName, int position, bool isNull)
    {
        string nullInfo = isNull ? " (null)" : "";
        logIndent($"Rule `{ruleName}` created memoization cache on position {position}{nullInfo}");
    }

    [Conditional(parser_verbose)]
    protected void LogRuleExiting(string ruleName) => logIndent($"Exiting rule `{ruleName}`");

    [Conditional(parser_verbose)]
    protected void LogAlternativeEntered(string alternativeSourceText)
        => logIndent($"Trying to match: \"{alternativeSourceText}\"");

    [Conditional(parser_verbose)]
    protected void LogAlternativeFailed(string alternativeSourceText)
        => logIndent($"Not matched: \"{alternativeSourceText}\"");

    [Conditional(parser_verbose)]
    protected void LogAlternativeSucceed(string alternativeSourceText)
        => logIndent($"Matched: \"{alternativeSourceText}\"");

    [Conditional(parser_verbose)]
    protected void LogIncreaseLevel() => logIndentationLevel++;

    [Conditional(parser_verbose)]
    protected void LogDecreaseLevel() => logIndentationLevel--;

    [Conditional(parser_verbose)]
    protected void LogStartGrow(string ruleName) => logIndent($"Starting to grow `{ruleName}`; first memo set to null");

    [Conditional(parser_verbose)]
    protected void LogNextGrow(string ruleName) => logIndent($"Next grow iteration of the `{ruleName}`");

    [Conditional(parser_verbose)]
    protected void LogEndGrow(string ruleName, bool isNull)
    {
        string result = isNull ? "null" : "succeed";
        logIndent($"End to grow `{ruleName}`, result: {result}");
    }

    [Conditional(parser_verbose)]
    protected void LogLeftRecursionRuleEntered(string ruleName)
        => logIndent($"The wrapper for `{ruleName}` is entered");

    private int logIndentationLevel = 0;

    private void logIndent(string message)
    {
#if PARSER_VERBOSE
        Console.Write($"[{logIndentationLevel,3}]");
        for (int i = 0; i < logIndentationLevel; i++)
        {
            Console.Write(log_indent_string);
        }
        var token = tokenStream.PeekOrDefault();
        Console.WriteLine($"{message}; ({tokenStream.Index}: {token?.Type.ToString() ?? "None"}-'{token?.RawString}')");
#endif
    }
}
