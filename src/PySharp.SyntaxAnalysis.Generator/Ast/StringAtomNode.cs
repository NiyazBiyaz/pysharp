using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

public record StringAtomNode : AtomNode
{
    public StringAtomNode(string value)
    {
        Debug.Assert(!StringParser.HasByte(value));
        Value = StringParser.ParseQuotedString(value);
    }

    public override string ToString() => $"StringAtomNode('{Value}')";
}
