using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

public record AliasNode : GreenNode
{
    public string OldValue { get; set; }
    public string NewValue { get; set; }

    public AliasNode(string oldVal, string newVal)
    {
        OldValue = oldVal;
        NewValue = newVal;
    }
}
