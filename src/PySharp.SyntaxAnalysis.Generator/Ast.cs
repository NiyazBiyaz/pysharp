using System.Collections.Immutable;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal interface IGroup : IRedView
{
    ImmutableArray<AlternativeView> Alternatives { get; }
    ViewArray<RedView> AstAlternatives { get; }
    GroupDecoratorView? Decorator { get; }

    internal GroupIdentifier Identifier => new(
        Decorator?.Value.RawString,
        new([.. Alternatives.Select(a => a.Identifier)]));
}

internal partial class GroupAtomView : IGroup
{
    ImmutableArray<AlternativeView> IGroup.Alternatives => Alternatives;

    ViewArray<RedView> IGroup.AstAlternatives => AstAlternatives;

    GroupDecoratorView? IGroup.Decorator => Decorator;
}

internal partial class OptionalGroupView : IGroup
{
    ImmutableArray<AlternativeView> IGroup.Alternatives => Alternatives;

    ViewArray<RedView> IGroup.AstAlternatives => AstAlternatives;

    GroupDecoratorView? IGroup.Decorator => Decorator;
}

internal partial class MoleculeView
{
    internal QuantifierKind GetQuantifier() => this switch
    {
        AtomMoleculeView => QuantifierKind.Expect,
        OptionalGroupView or OptionalView => QuantifierKind.Optional,
        RepeatOneMoreView or RepeatZeroMoreView => QuantifierKind.Repeat,
        PositiveLookaheadView or NegativeLookaheadView => QuantifierKind.Lookahead,
        GatherView => QuantifierKind.Gather,
        CutView => QuantifierKind.Cut,
        _ => throw new ArgumentOutOfRangeException(),
    };

    internal string GetName() => this switch
    {
        AtomMoleculeView hydrogen => hydrogen.Atom.GetName(),
        OptionalGroupView og => og.RecoverText(), // Se below about GroupAtomView
        OptionalView opt => opt.Atom.GetName(),
        RepeatOneMoreView r1 => r1.Atom.GetName(),
        RepeatZeroMoreView r0 => r0.Atom.GetName(),
        PositiveLookaheadView p => p.Atom.GetName(),
        NegativeLookaheadView n => n.Atom.GetName(),
        CutView => "~",
        GatherView g => g.ValueAtom.GetName() + g.ValueAtom.GetName(),
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal partial class AtomView
{
    internal string GetName() => this switch
    {
        StringAtomView s => s.Value.RawString,
        NameAtomView n => n.Value.RawString,
        // It's not good to use just group source text, but i think it's enough rare to have
        // 2 exactly same groups that contain 2 exactly same groups with the different spaces.
        GroupAtomView g => g.RecoverText(),
        _ => throw new ArgumentOutOfRangeException(),
    };
}

internal partial class AlternativeView
{
    internal HashableArray<QuantifierKind> GetQuantifierArray() => new(Molecules.Select(m => m.GetQuantifier()));
    internal HashableArray<string> GetNameArray() => new(Molecules.Select(m => m.GetName()));
    internal string? GetActionName() => Action switch
    {
        null => "null",
        InferredActionView => "new",
        NamedActionView n => n.Name.RawString,
        _ => throw new ArgumentOutOfRangeException(),
    };

    internal AlternativeIdentifier Identifier => new(
        GetNameArray(),
        GetQuantifierArray(),
        GetActionName(),
        Action?.GetFieldNameArray() ?? new([]),
        Action?.GetVariableNameArray() ?? new([]));
}

internal partial class ActionView
{
    internal HashableArray<string> GetFieldNameArray() => new(Arguments?.Value.Select(a => a.Field.RawString) ?? []);
    internal HashableArray<string> GetVariableNameArray() => new(Arguments?.Value.Select(a => a.Variable.RawString) ?? []);
}
