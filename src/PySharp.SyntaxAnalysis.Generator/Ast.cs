using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal interface IGroup : IGreenNode
{
    ImmutableArray<AlternativeNode> Alternatives { get; }
    NodeArray<GreenNode> AstAlternatives { get; }
    GroupDecoratorNode? Decorator { get; }

    internal GroupIdentifier Identifier => new(
        Decorator?.Value.RawString,
        new([.. Alternatives.Select(a => a.Identifier)]));
}

internal partial record GroupAtomNode : IGroup
{
    ImmutableArray<AlternativeNode> IGroup.Alternatives => Alternatives;

    NodeArray<GreenNode> IGroup.AstAlternatives => AstAlternatives;

    GroupDecoratorNode? IGroup.Decorator => Decorator;
}

internal partial record OptionalGroupNode : IGroup
{
    ImmutableArray<AlternativeNode> IGroup.Alternatives => Alternatives;

    NodeArray<GreenNode> IGroup.AstAlternatives => AstAlternatives;

    GroupDecoratorNode? IGroup.Decorator => Decorator;
}

internal partial record MoleculeNode
{
    internal QuantifierKind GetQuantifier() => this switch
    {
        AtomMoleculeNode => QuantifierKind.Expect,
        OptionalGroupNode or OptionalNode => QuantifierKind.Optional,
        RepeatOneMoreNode or RepeatZeroMoreNode => QuantifierKind.Repeat,
        PositiveLookaheadNode or NegativeLookaheadNode => QuantifierKind.Lookahead,
        GatherNode => QuantifierKind.Gather,
        CutNode => QuantifierKind.Cut,
        _ => throw new ArgumentOutOfRangeException(),
    };

    internal string GetName() => this switch
    {
        AtomMoleculeNode hydrogen => hydrogen.Atom.GetName(),
        OptionalGroupNode => "optGroup", // Se below about GroupAtomNode
        OptionalNode opt => opt.Atom.GetName(),
        RepeatOneMoreNode r1 => r1.Atom.GetName(),
        RepeatZeroMoreNode r0 => r0.Atom.GetName(),
        PositiveLookaheadNode p => p.Atom.GetName(),
        NegativeLookaheadNode n => n.Atom.GetName(),
        CutNode => "~",
        GatherNode g => g.ValueAtom.GetName() + g.ValueAtom.GetName(),
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal partial record AtomNode
{
    internal string GetName() => this switch
    {
        StringAtomNode s => s.Value.RawString,
        NameAtomNode n => n.Value.RawString,
        // It's not good to make just group, but i think it's rare to have 2 exactly same
        // groups that contain 2 exactly same groups with the different spaces.
        GroupAtomNode => "Group",
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal partial record AlternativeNode
{
    internal HashableArray<QuantifierKind> GetQuantifierArray() => new(Molecules.Select(m => m.GetQuantifier()));
    internal HashableArray<string> GetNameArray() => new(Molecules.Select(m => m.GetName()));
    internal string? GetActionName() => Action switch
    {
        null => "null",
        InferredActionNode => "new",
        NamedActionNode n => n.Name.RawString,
        _ => throw new ArgumentOutOfRangeException(),
    };

    internal AlternativeIdentifier Identifier => new(
        GetNameArray(),
        GetQuantifierArray(),
        GetActionName(),
        Action?.GetFieldNameArray() ?? new([]),
        Action?.GetVariableNameArray() ?? new([]));
}

internal partial record ActionNode
{
    internal HashableArray<string> GetFieldNameArray() => new(Arguments?.Value.Select(a => a.Field.RawString) ?? []);
    internal HashableArray<string> GetVariableNameArray() => new(Arguments?.Value.Select(a => a.Variable.RawString) ?? []);
}
