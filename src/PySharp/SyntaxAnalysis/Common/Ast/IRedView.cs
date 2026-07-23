namespace PySharp.SyntaxAnalysis.Common.Ast;

public interface IRedView
{
    int FullPosition { get; }
    int EndPosition { get; }

    int Position { get; }

    IRedView? Parent { get; }

    SyntaxViewTree SyntaxTree { get; }

    Position2D Position2D { get; }
    Position2D FullPosition2D { get; }
    Position2D EndPosition2D { get; }

    bool IsArray { get; }

    string PrettyPrint();
    string RecoverText();
}
