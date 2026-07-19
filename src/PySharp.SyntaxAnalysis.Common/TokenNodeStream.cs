using PySharp.SyntaxAnalysis.Tokens;
using PySharp.SyntaxAnalysis.Common.Ast;
using System.Diagnostics;

namespace PySharp.SyntaxAnalysis.Common;

public class TokenNodeStream(ITokenizer tokenizer) : ITokenNodeStream
{
    private readonly List<TokenNode> tokens = [];
    private readonly ITokenizer tokenizer = tokenizer;

    private TokenPosition lastTokenEndPosition = default;

    public int Index
    {
        get;
        set
        {
            if (value == field)
                return;

            Debug.Assert(value >= 0 && value <= tokens.Count, $"value={value} tokens.Count={tokens.Count}");

            field = value;
        }
    } = 0;

    public TokenNode GetAndAdvance()
    {
        var tok = PeekToken();
        Index += 1;
        return tok;
    }

    public TokenNode PeekToken()
    {
        if (Index == tokens.Count)
        {
            List<TokenNode> trivias = [];
            TokenNode node;
            do
            {
                Debug.Assert(!tokenizer.ShouldStop, $"Tokens count: {tokens.Count}, Index: {Index}");

                var tok = tokenizer.ReadNext();
                if (tok.Type.IsTrivia)
                {
                    node = new(tok, [], lastTokenEndPosition);
                    trivias.Add(node);
                }
                else if (tok.Type.IsError)
                {
                    Debug.Assert(tokenizer.Error != TokenizerError.NoError);

                    node = new InvalidTokenNode(tok, [], lastTokenEndPosition, tokenizer.ErrorMessage, tokenizer.Error);
                    trivias.Add(node);
                }
                else
                {
                    node = new(tok, trivias, lastTokenEndPosition);
                }

                lastTokenEndPosition = tok.End;
            }
            while (node.Type.IsTrivia || node.Type.IsError);

            tokens.Add(node);
        }

        return tokens[Index];
    }
}
