using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public record TokenNode : GreenNode
{
    public override NodeArray<GreenNode>? Children => null;
    public override int FullWidth { get; }
    public int Width { get; }
    public TokenType Type { get; }
    public NodeArray<TokenNode> Leading { get; }
    public string RawString { get; }

    public override int? TriviaWidth => FullWidth - Width;

    public TokenNode(in Token token, IEnumerable<TokenNode> leading)
    {
        Type = token.Type;
        Width = token.Lexeme.Length;

        int acc = 0;
        foreach (var node in leading)
            acc += node.Width;

        FullWidth = acc + Width;

        Leading = new(leading);
        RawString = token.Lexeme.ToString(); // TODO: add caching.
    }

    public override IRedView GetView(int position, IRedView? parent) => new TokenView(this, position, parent);

    public override string ToString() => $"TokenNode({Type}, '{RawString}', {Width})";

    public override string RecoverText()
    {
        var builder = new StringBuilder();
        foreach (var trivia in Leading)
            builder.Append(trivia.RawString);

        builder.Append(RawString);

        return builder.ToString();
    }

    public override void AcceptRecoverText(StringBuilder builder)
    {
        foreach (var trivia in Leading)
            builder.Append(trivia.RawString);

        builder.Append(RawString);
    }

    public override void AcceptPrettyPrint(StringBuilder builder, int indentation)
    {
        builder.Append("Token(");
        builder.Append($"Type: {Type}, RawString: `{RawString.ReplaceLineEndings("\\n")}`");
        builder.Append(')');
    }
}
