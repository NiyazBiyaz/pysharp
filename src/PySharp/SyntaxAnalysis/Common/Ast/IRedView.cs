namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IRedView
{
    int Position { get; }
    int EndPosition { get; }
    IRedView? Parent { get; }

    bool IsArray { get; }

    string PrettyPrint();
    string RecoverText();
}
