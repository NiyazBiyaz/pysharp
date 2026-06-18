// This file was automatically generated from Meta.ebnf
// Bau bau!
#nullable enable
using PySharp.SyntaxAnalysis.Tokens;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal class GrammarParser(ITokenNodeStream tokenStream) : BaseParser<GrammarNode>(tokenStream)
{
    public override GrammarNode? Parse() => rule_Start();
    protected override HashSet<string> Keywords { get; } = [];
    #region Start
    GrammarNode? rule_Start()
    {
        int __mark = Mark();
        {
            //  Meta+ Rule+ EndOfFile
            NodeArray<MetadataNode>? metaPlus;
            NodeArray<RuleNode>? rulePlus;
            TokenNode? endoffile;
            if (
                (metaPlus = Repeat(rule_Meta, 1)) is not null
                &&
                (rulePlus = Repeat(rule_Rule, 1)) is not null
                &&
                (endoffile = Expect(TokenType.EndOfFile)) is not null
            )
            {
                return  new GrammarNode(metaPlus, rulePlus)
                {
                    Children = new NodeArray<GreenNode>([new NodeArrayWrapNode(metaPlus), new NodeArrayWrapNode(rulePlus), endoffile])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Meta
    MetadataNode? rule_Meta()
    {
        int __mark = Mark();
        {
            //  "@" Name StringLiteral NewLine
            TokenNode? at;
            TokenNode? name;
            TokenNode? stringliteral;
            TokenNode? newline;
            if (
                (at = Expect(TokenType.At)) is not null
                &&
                (name = Expect(TokenType.Name)) is not null
                &&
                (stringliteral = Expect(TokenType.StringLiteral)) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return  new MetadataNode(name.RawString, StringParser.ParseQuotedString(stringliteral.RawString))
                {
                    Children = new NodeArray<GreenNode>([at, name, stringliteral, newline])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region _PolyGenAnonymousType0
    _PolyGenAnonymousType0? rule__PolyGenAnonymousType0()
    {
        int __mark = Mark();
        {
            // "|" Alternative NewLine
            TokenNode? vertbar;
            AlternativeNode? alternative;
            TokenNode? newline;
            if (
                (vertbar = Expect(TokenType.VertBar)) is not null
                &&
                (alternative = rule_Alternative()) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return new _PolyGenAnonymousType0(vertbar, alternative, newline)
                {
                    Children = new NodeArray<GreenNode>([vertbar, alternative, newline])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Rule
    RuleNode? rule_Rule()
    {
        int __mark = Mark();
        {
            //  "@" Name NewLine Name -TypeSpec ":" NewLine Indent ("|" Alternative NewLine)+ Dedent
            TokenNode? at;
            TokenNode? name;
            TokenNode? newline;
            TokenNode? name1;
            TypeSpecNode? typespec;
            TokenNode? colon;
            TokenNode? newline1;
            TokenNode? indent;
            NodeArray<_PolyGenAnonymousType0>? groupPlus;
            TokenNode? dedent;
            if (
                (at = Expect(TokenType.At)) is not null
                &&
                (name = Expect(TokenType.Name)) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
                &&
                (name1 = Expect(TokenType.Name)) is not null
                &&
                ((typespec = rule_TypeSpec()) is not null || true) // Optional
                &&
                (colon = Expect(TokenType.Colon)) is not null
                &&
                (newline1 = Expect(TokenType.NewLine)) is not null
                &&
                (indent = Expect(TokenType.Indent)) is not null
                &&
                (groupPlus = Repeat(rule__PolyGenAnonymousType0, 1)) is not null
                &&
                (dedent = Expect(TokenType.Dedent)) is not null
            )
            {
                List<GreenNode> __children = [at, name, newline, name1, typespec!, colon, newline1, indent, new NodeArrayWrapNode(groupPlus), dedent];
                __children.RemoveAll(static __node => __node is null);
                return  new DecoratedRuleNode(name.RawString, name1.RawString, typespec, [.. groupPlus.Select(g => g.alternative)])
                {
                    Children = new NodeArray<GreenNode>(__children)
                };
            }
        }
        Reset(__mark);
        {
            //  "@" Name NewLine Name -TypeSpec ":" Alternative NewLine
            TokenNode? at;
            TokenNode? name;
            TokenNode? newline;
            TokenNode? name1;
            TypeSpecNode? typespec;
            TokenNode? colon;
            AlternativeNode? alternative;
            TokenNode? newline1;
            if (
                (at = Expect(TokenType.At)) is not null
                &&
                (name = Expect(TokenType.Name)) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
                &&
                (name1 = Expect(TokenType.Name)) is not null
                &&
                ((typespec = rule_TypeSpec()) is not null || true) // Optional
                &&
                (colon = Expect(TokenType.Colon)) is not null
                &&
                (alternative = rule_Alternative()) is not null
                &&
                (newline1 = Expect(TokenType.NewLine)) is not null
            )
            {
                List<GreenNode> __children = [at, name, newline, name1, typespec!, colon, alternative, newline1];
                __children.RemoveAll(static __node => __node is null);
                return  new DecoratedRuleNode(name.RawString, name1.RawString, typespec, [alternative])
                {
                    Children = new NodeArray<GreenNode>(__children)
                };
            }
        }
        Reset(__mark);
        {
            //  Name -TypeSpec ":" NewLine Indent ("|" Alternative NewLine)+ Dedent
            TokenNode? name;
            TypeSpecNode? typespec;
            TokenNode? colon;
            TokenNode? newline;
            TokenNode? indent;
            NodeArray<_PolyGenAnonymousType0>? groupPlus;
            TokenNode? dedent;
            if (
                (name = Expect(TokenType.Name)) is not null
                &&
                ((typespec = rule_TypeSpec()) is not null || true) // Optional
                &&
                (colon = Expect(TokenType.Colon)) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
                &&
                (indent = Expect(TokenType.Indent)) is not null
                &&
                (groupPlus = Repeat(rule__PolyGenAnonymousType0, 1)) is not null
                &&
                (dedent = Expect(TokenType.Dedent)) is not null
            )
            {
                List<GreenNode> __children = [name, typespec!, colon, newline, indent, new NodeArrayWrapNode(groupPlus), dedent];
                __children.RemoveAll(static __node => __node is null);
                return  new RuleNode(name.RawString, typespec, [.. groupPlus.Select(g => g.alternative)])
                {
                    Children = new NodeArray<GreenNode>(__children)
                };
            }
        }
        Reset(__mark);
        {
            //  Name -TypeSpec ":" Alternative NewLine
            TokenNode? name;
            TypeSpecNode? typespec;
            TokenNode? colon;
            AlternativeNode? alternative;
            TokenNode? newline;
            if (
                (name = Expect(TokenType.Name)) is not null
                &&
                ((typespec = rule_TypeSpec()) is not null || true) // Optional
                &&
                (colon = Expect(TokenType.Colon)) is not null
                &&
                (alternative = rule_Alternative()) is not null
                &&
                (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                List<GreenNode> __children = [name, typespec!, colon, alternative, newline];
                __children.RemoveAll(static __node => __node is null);
                return  new RuleNode(name.RawString, typespec, [alternative])
                {
                    Children = new NodeArray<GreenNode>(__children)
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region TypeSpec
    TypeSpecNode? rule_TypeSpec()
    {
        int __mark = Mark();
        {
            //  "[" Name "]"
            TokenNode? leftsquarebracket;
            TokenNode? name;
            TokenNode? rightsquarebracket;
            if (
                (leftsquarebracket = Expect(TokenType.LeftSquareBracket)) is not null
                &&
                (name = Expect(TokenType.Name)) is not null
                &&
                (rightsquarebracket = Expect(TokenType.RightSquareBracket)) is not null
            )
            {
                return  new TypeSpecNode(name.RawString)
                {
                    Children = new NodeArray<GreenNode>([leftsquarebracket, name, rightsquarebracket])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Alternative
    AlternativeNode? rule_Alternative()
    {
        int __mark = Mark();
        {
            //  Molecule+ -Action
            NodeArray<MoleculeNode>? moleculePlus;
            ActionNode? action;
            if (
                (moleculePlus = Repeat(rule_Molecule, 1)) is not null
                &&
                ((action = rule_Action()) is not null || true) // Optional
            )
            {
                List<GreenNode> __children = [new NodeArrayWrapNode(moleculePlus), action!];
                __children.RemoveAll(static __node => __node is null);
                return  new AlternativeNode(moleculePlus, action)
                {
                    Children = new NodeArray<GreenNode>(__children)
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Molecule
    MoleculeNode? rule_Molecule()
    {
        int __mark = Mark();
        {
            //  "&" Atom
            TokenNode? ampersand;
            AtomNode? atom;
            if (
                (ampersand = Expect(TokenType.Ampersand)) is not null
                &&
                (atom = rule_Atom()) is not null
            )
            {
                return  new LookaheadNode(atom, true)
                {
                    Children = new NodeArray<GreenNode>([ampersand, atom])
                };
            }
        }
        Reset(__mark);
        {
            //  "!" Atom
            TokenNode? exclamation;
            AtomNode? atom;
            if (
                (exclamation = Expect(TokenType.Exclamation)) is not null
                &&
                (atom = rule_Atom()) is not null
            )
            {
                return  new LookaheadNode(atom, false)
                {
                    Children = new NodeArray<GreenNode>([exclamation, atom])
                };
            }
        }
        Reset(__mark);
        {
            //  "-" Atom
            TokenNode? minus;
            AtomNode? atom;
            if (
                (minus = Expect(TokenType.Minus)) is not null
                &&
                (atom = rule_Atom()) is not null
            )
            {
                return  new OptionalNode(atom)
                {
                    Children = new NodeArray<GreenNode>([minus, atom])
                };
            }
        }
        Reset(__mark);
        {
            //  Atom "*"
            AtomNode? atom;
            TokenNode? star;
            if (
                (atom = rule_Atom()) is not null
                &&
                (star = Expect(TokenType.Star)) is not null
            )
            {
                return  new RepeatZeroMoreNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom, star])
                };
            }
        }
        Reset(__mark);
        {
            //  Atom "+"
            AtomNode? atom;
            TokenNode? plus;
            if (
                (atom = rule_Atom()) is not null
                &&
                (plus = Expect(TokenType.Plus)) is not null
            )
            {
                return  new RepeatOneMoreNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom, plus])
                };
            }
        }
        Reset(__mark);
        {
            //  Atom
            AtomNode? atom;
            if (
                (atom = rule_Atom()) is not null
            )
            {
                return  new AtomMoleculeNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Atom
    AtomNode? rule_Atom()
    {
        int __mark = Mark();
        {
            //  "(" Alternative ")"
            TokenNode? leftparen;
            AlternativeNode? alternative;
            TokenNode? rightparen;
            if (
                (leftparen = Expect(TokenType.LeftParen)) is not null
                &&
                (alternative = rule_Alternative()) is not null
                &&
                (rightparen = Expect(TokenType.RightParen)) is not null
            )
            {
                return  new GroupAtomNode([alternative])
                {
                    Children = new NodeArray<GreenNode>([leftparen, alternative, rightparen])
                };
            }
        }
        Reset(__mark);
        {
            //  Name
            TokenNode? name;
            if (
                (name = Expect(TokenType.Name)) is not null
            )
            {
                return  new NameAtomNode(name.RawString)
                {
                    Children = new NodeArray<GreenNode>([name])
                };
            }
        }
        Reset(__mark);
        {
            //  StringLiteral
            TokenNode? stringliteral;
            if (
                (stringliteral = Expect(TokenType.StringLiteral)) is not null
            )
            {
                return  new StringAtomNode(stringliteral.RawString)
                {
                    Children = new NodeArray<GreenNode>([stringliteral])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region Action
    ActionNode? rule_Action()
    {
        int __mark = Mark();
        {
            //  "->" ActionStuff+
            TokenNode? rightarrow;
            NodeArray<TokenNode>? actionstuffPlus;
            if (
                (rightarrow = Expect(TokenType.RightArrow)) is not null
                &&
                (actionstuffPlus = Repeat(rule_ActionStuff, 1)) is not null
            )
            {
                return  new ActionNode(actionstuffPlus.RecoverText())
                {
                    Children = new NodeArray<GreenNode>([rightarrow, new NodeArrayWrapNode(actionstuffPlus)])
                };
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
    #region ActionStuff
    TokenNode? rule_ActionStuff()
    {
        int __mark = Mark();
        {
            //  Name
            TokenNode? name;
            if (
                (name = Expect(TokenType.Name)) is not null
            )
            {
                return name;
            }
        }
        Reset(__mark);
        {
            //  StringLiteral
            TokenNode? stringliteral;
            if (
                (stringliteral = Expect(TokenType.StringLiteral)) is not null
            )
            {
                return stringliteral;
            }
        }
        Reset(__mark);
        {
            //  "("
            TokenNode? leftparen;
            if (
                (leftparen = Expect(TokenType.LeftParen)) is not null
            )
            {
                return leftparen;
            }
        }
        Reset(__mark);
        {
            //  ")"
            TokenNode? rightparen;
            if (
                (rightparen = Expect(TokenType.RightParen)) is not null
            )
            {
                return rightparen;
            }
        }
        Reset(__mark);
        {
            //  ","
            TokenNode? comma;
            if (
                (comma = Expect(TokenType.Comma)) is not null
            )
            {
                return comma;
            }
        }
        Reset(__mark);
        {
            //  "."
            TokenNode? dot;
            if (
                (dot = Expect(TokenType.Dot)) is not null
            )
            {
                return dot;
            }
        }
        Reset(__mark);
        {
            //  ">"
            TokenNode? greater;
            if (
                (greater = Expect(TokenType.Greater)) is not null
            )
            {
                return greater;
            }
        }
        Reset(__mark);
        {
            //  "<"
            TokenNode? less;
            if (
                (less = Expect(TokenType.Less)) is not null
            )
            {
                return less;
            }
        }
        Reset(__mark);
        {
            //  "="
            TokenNode? equal;
            if (
                (equal = Expect(TokenType.Equal)) is not null
            )
            {
                return equal;
            }
        }
        Reset(__mark);
        {
            //  "=="
            TokenNode? eqequal;
            if (
                (eqequal = Expect(TokenType.EqEqual)) is not null
            )
            {
                return eqequal;
            }
        }
        Reset(__mark);
        {
            //  ">="
            TokenNode? greaterequal;
            if (
                (greaterequal = Expect(TokenType.GreaterEqual)) is not null
            )
            {
                return greaterequal;
            }
        }
        Reset(__mark);
        {
            //  "<="
            TokenNode? lessequal;
            if (
                (lessequal = Expect(TokenType.LessEqual)) is not null
            )
            {
                return lessequal;
            }
        }
        Reset(__mark);
        {
            //  "!"
            TokenNode? exclamation;
            if (
                (exclamation = Expect(TokenType.Exclamation)) is not null
            )
            {
                return exclamation;
            }
        }
        Reset(__mark);
        {
            //  "["
            TokenNode? leftsquarebracket;
            if (
                (leftsquarebracket = Expect(TokenType.LeftSquareBracket)) is not null
            )
            {
                return leftsquarebracket;
            }
        }
        Reset(__mark);
        {
            //  "]"
            TokenNode? rightsquarebracket;
            if (
                (rightsquarebracket = Expect(TokenType.RightSquareBracket)) is not null
            )
            {
                return rightsquarebracket;
            }
        }
        Reset(__mark);
        return null;
    }
    #endregion
}
#region type _PolyGenAnonymousType0
internal record _PolyGenAnonymousType0 : GreenNode
{
    internal TokenNode vertbar { get; private init; }
    internal AlternativeNode alternative { get; private init; }
    internal TokenNode newline { get; private init; }
    internal _PolyGenAnonymousType0(TokenNode vertbar, AlternativeNode alternative, TokenNode newline)
    {
        this.vertbar = vertbar;
        this.alternative = alternative;
        this.newline = newline;
    }
}
#endregion
