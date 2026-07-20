using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Common;

public interface ITokenNodeStream
{
    int Index { get; set; }

    TokenNode GetAndAdvance();
    TokenNode PeekToken();
}
