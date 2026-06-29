using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record AtomNode : GreenNode;

internal record GroupAtomNode : AtomNode
{
    internal AlternativeNode Alternative => (AlternativeNode)Children![1];
}

internal record NameAtomNode : AtomNode
{
    internal TokenNode Value => (TokenNode)Children![0];
}

internal record StringAtomNode : AtomNode
{
    internal TokenNode Value => (TokenNode)Children![0];
}
