using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record AtomNode : GreenNode;

internal record GroupAtomNode(NodeArray<AlternativeNode> Alternatives) : AtomNode;

internal record NameAtomNode(string Value) : AtomNode
{
    public override string ToString() => $"NameAtomNode({Value})";
}

internal record StringAtomNode(string Value) : AtomNode
{
    public string Parsed => StringParser.ParseQuotedString(Value);

    public override string ToString() => $"StringAtomNode(`{Value}`)";
}

