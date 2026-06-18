using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal record StringAtomNode : AtomNode
{
    public string Value { get; private init; }

    public StringAtomNode(string value)
    {
        Debug.Assert(!StringParser.HasPrefix(value));
        Value = value;
    }

    public string Parsed => StringParser.ParseQuotedString(Value);

    public override string ToString() => $"StringAtomNode(`{Value}`)";
}
