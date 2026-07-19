using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IRedView
{
    TokenPosition Position { get; }
    IRedView? Parent { get; }
    TokenPosition EndPosition { get; }

    bool IsArray { get; }

    string PrettyPrint();
    string RecoverText();
}
