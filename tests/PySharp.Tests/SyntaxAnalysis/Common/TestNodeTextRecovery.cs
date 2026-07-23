using PySharp.SyntaxAnalysis.Common.Ast;
using static PySharp.SyntaxAnalysis.Tokens.TokenType;

namespace PySharp.Tests.SyntaxAnalysis.Common;

public class TestNodeTextRecovery()
{
    // NOTE: All tokens should be independent from their metadata such TokenType while recovering text.

    private static readonly List<(TokenNode, string)> test_tokens =
    [
        (new(new(At, "if"), []), "if"),
        (new(new(At, "else"), []), "else"),
        (new(new(At, "123"), []), "123"),
        (new(new(At, "if"), [tr("  ")]), "  if"),
        (new(new(At, "if"), [tr("\n"), tr("    ")]), "\n    if"),
        (new(new(At, "if"), [tr("# bau bau"), tr("\n"), tr("    ")]), "# bau bau\n    if"),
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void TestTokenNode(int index)
    {
        (var tok, string exp) = test_tokens[index - 1];
        string act = tok.RecoverText();
        Assert.Equal(exp, act);
    }

    [Fact]
    public void TestCompoundNode()
    {
        var expected = "[1, 2,\n 3, 4]";
        var node = new ListNode()
        {
            Children =
            new NodeArray<GreenNode>
            ([
                new TokenNode(new(LeftSquareBracket, "["), []),
                new ItemNode()
                {
                    Children =
                    new NodeArray<GreenNode>
                    ([
                        new TokenNode(new(Number, "1"), []),
                        new TokenNode(new(Comma, ","), [])
                    ])
                },
                new ItemNode()
                {
                    Children = new NodeArray<GreenNode>
                    ([
                        new TokenNode(new(Number, "2"), [tr(" ")]),
                        new TokenNode(new(Comma, ","), [])
                    ])
                },
                new ItemNode()
                {
                    Children = new NodeArray<GreenNode>
                    ([
                        new TokenNode(new(Number, "3"), [tr("\n"), tr(" ")]),
                        new TokenNode(new(Comma, ","), []),
                    ])
                },
                new ItemNode()
                {
                    Children = new NodeArray<GreenNode>
                    ([
                        new TokenNode(new(Number, "4"), [tr(" ")]),
                    ])
                },
                new TokenNode(new(RightSquareBracket, "]"), []),
            ]),
        };

        string actual = node.RecoverText();
        Assert.Equal(expected, actual);
    }

    record ListNode : GreenNode
    {
        public override IRedView GetView(int position, IRedView? parent) => throw new NotImplementedException();
    }
    record ItemNode : GreenNode
    {
        public override IRedView GetView(int position, IRedView? parent) => throw new NotImplementedException();
    }

    // To avoid boilerplate for initialized it with zero all time.
    private static TokenNode tr(string value) => new(new(WhiteSpace, value), []);
}
