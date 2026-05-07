// This file was generated from 'Meta.ebnf'.
// СВИНОЙ ШАР
// ⠀⠀⠀⠀⠀⠀⠀⠀⣠⣤⣴⣾⣿⡿⣟⣯⢿⠾⣙⠒⠢⢄⡀⠀⠀⠀⠀⠀⠀⠀
// ⠀⠀⠀⠀⢀⣠⣶⣿⣿⣿⣿⡿⣷⣟⡿⣞⣯⣿⣭⠷⣆⡄⡈⠑⠦⡀⠀⠀⠀⠀
// ⠀⠀⠀⣴⣿⣿⣿⣿⣿⡿⣿⣽⡿⣾⣿⢿⣻⡾⣽⡻⣭⢷⡑⢦⡀⠉⢦⡀⠀⠀
// ⠀⢀⣾⣿⣿⣿⣿⣿⡏⠉⠁⠉⠉⠛⣿⣿⠟⠉⠉⠁⠀⠉⢻⡆⡕⢣⡀⢳⡀⠀
// ⠀⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣶⣶⣶⣿⣷⣶⣤⣦⣶⣦⣤⣌⡳⡘⢧⡘⡄⢳⠀
// ⢸⣿⣿⣿⣿⣿⣿⣿⣿⣏⠉⠉⣿⡯⢽⣻⣿⣿⣍⠀⠉⣹⠏⠙⢿⣢⢝⡰⢈⣇
// ⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⣫⣵⠶⠶⠿⠶⣽⣿⣿⣿⣿⣀⣤⡾⣓⢮⡔⠈⠀
// ⣿⣿⣿⣿⣿⢻⢿⡻⣟⣿⢫⣷⣿⣿⣿⣿⣿⣶⣽⣿⣿⣿⣿⠆⢻⡹⡖⣍⠂⠀
// ⣿⣿⣿⣿⣿⢯⣾⣿⣿⡇⣿⣇⣀⣿⣿⣦⣤⣿⡇⣿⡿⠛⣿⠠⢡⢳⡙⠦⠁⠀
// ⠸⣿⣿⣿⣯⢿⣳⣿⣿⣿⢙⠿⠿⠿⠛⠻⠟⠋⠁⢁⢈⣾⢮⠑⣎⡙⢂⠁⠀
// ⠀⢿⣿⣳⢯⣟⣯⡷⢯⣿⣯⡴⣤⣤⣤⣤⣴⣶⡿⣡⣾⢌⠢⡙⠤⠑⠀⠀⠀
// ⠀⠈⢿⣟⡿⣞⣷⣻⣽⢯⣿⣾⣭⣭⣭⣭⣷⣾⣿⢮⢋⢆⠓⡨⠐⠁⡠⠀⠀
// ⠀⠀⠀⠻⣿⡽⢯⣟⡾⢯⢞⡯⣝⢣⠏⢮⠑⠣⠍⢎⠳⠌⠢⠁⢐⡤⠛⠀⠀⠀
// ⠀⠀⠀⠀⠀⠛⢿⣘⠹⢎⠳⡜⢤⢃⠎⠤⢉⠐⠠⠀⠀⠀⣠⠖⠋⠀⠀⠀⠀⠀
// ⠀⠀⠀⠀⠀⠀⠀⠈⠉⠓⠒⠀⠂⠈⠈⠀⠀⠀⠀⠀⠀⠉⠀⠀⠀
#nullable enable
using PySharp.SyntaxAnalysis.Tokens;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal class GrammarParser(ITokenNodeStream tokenStream) : BaseParser<GrammarNode>(tokenStream)
{
    GrammarNode? rule_Start()
    {
        int __mark = Mark();
        // Meta+ Alias* Rule+ EndOfFile
        {
            NodeArray<MetadataNode>? metaPlus;
            NodeArray<AliasNode>? aliasStar;
            NodeArray<RuleNode>? rulePlus;
            TokenNode? endoffile;
            if (true
                && (metaPlus = Repeat(rule_Meta, 1)) is not null
                && (aliasStar = Repeat(rule_Alias, 0)) is not null
                && (rulePlus = Repeat(rule_Rule, 1)) is not null
                && (endoffile = Expect(TokenType.EndOfFile)) is not null
            )
            {
                return new GrammarNode(metaPlus, aliasStar, rulePlus)
                {
                    Children = new NodeArray<GreenNode>([new NodeArrayWrapNode(metaPlus), new NodeArrayWrapNode(aliasStar), new NodeArrayWrapNode(rulePlus), endoffile])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    MetadataNode? rule_Meta()
    {
        int __mark = Mark();
        // "@" Name StringLiteral NewLine
        {
            TokenNode? at;
            TokenNode? name;
            TokenNode? stringliteral;
            TokenNode? newline;
            if (true
                && (at = Expect(TokenType.At)) is not null
                && (name = Expect(TokenType.Name)) is not null
                && (stringliteral = Expect(TokenType.StringLiteral)) is not null
                && (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return new MetadataNode(name.RawString, StringParser.ParseQuotedString(stringliteral.RawString))
                {
                    Children = new NodeArray<GreenNode>([at, name, stringliteral, newline])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    AliasNode? rule_Alias()
    {
        int __mark = Mark();
        // "@" "alias" StringLiteral "->" Name NewLine
        {
            TokenNode? at;
            TokenNode? __str_tok1;
            TokenNode? stringliteral;
            TokenNode? rightarrow;
            TokenNode? name;
            TokenNode? newline;
            if (true
                && (at = Expect(TokenType.At)) is not null
                && (__str_tok1 = Expect("alias")) is not null
                && (stringliteral = Expect(TokenType.StringLiteral)) is not null
                && (rightarrow = Expect(TokenType.RightArrow)) is not null
                && (name = Expect(TokenType.Name)) is not null
                && (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return new AliasNode(StringParser.ParseQuotedString(stringliteral.RawString), name.RawString)
                {
                    Children = new NodeArray<GreenNode>([at, __str_tok1, stringliteral, rightarrow, name, newline])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    RuleNode? rule_Rule()
    {
        int __mark = Mark();
        // !"@" Name -TypeSpec ":" NewLine Indent Alternative+ Dedent
        {
            TokenNode? name;
            TypeSpecNode? typespec;
            TokenNode? colon;
            TokenNode? newline;
            TokenNode? indent;
            NodeArray<AlternativeNode>? alternativePlus;
            TokenNode? dedent;
            if (true
                && Lookahead(TokenType.At, false)
                && (name = Expect(TokenType.Name)) is not null
                && ((typespec = rule_TypeSpec()) is not null || true) // Optional
                && (colon = Expect(TokenType.Colon)) is not null
                && (newline = Expect(TokenType.NewLine)) is not null
                && (indent = Expect(TokenType.Indent)) is not null
                && (alternativePlus = Repeat(rule_Alternative, 1)) is not null
                && (dedent = Expect(TokenType.Dedent)) is not null
            )
            {
                return new RuleNode(name.RawString, typespec, alternativePlus)
                {
                    Children = new NodeArray<GreenNode>([name, typespec, colon, newline, indent, new NodeArrayWrapNode(alternativePlus), dedent])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    TypeSpecNode? rule_TypeSpec()
    {
        int __mark = Mark();
        // "[" Name "]"
        {
            TokenNode? leftsquarebracket;
            TokenNode? name;
            TokenNode? rightsquarebracket;
            if (true
                && (leftsquarebracket = Expect(TokenType.LeftSquareBracket)) is not null
                && (name = Expect(TokenType.Name)) is not null
                && (rightsquarebracket = Expect(TokenType.RightSquareBracket)) is not null
            )
            {
                return new TypeSpecNode(name.RawString)
                {
                    Children = new NodeArray<GreenNode>([leftsquarebracket, name, rightsquarebracket])
                };
            }
        }
        Reset(__mark);
        // "[" StringLiteral "]"
        {
            TokenNode? leftsquarebracket;
            TokenNode? stringliteral;
            TokenNode? rightsquarebracket;
            if (true
                && (leftsquarebracket = Expect(TokenType.LeftSquareBracket)) is not null
                && (stringliteral = Expect(TokenType.StringLiteral)) is not null
                && (rightsquarebracket = Expect(TokenType.RightSquareBracket)) is not null
            )
            {
                return new TypeSpecNode(StringParser.ParseQuotedString(stringliteral.RawString))
                {
                    Children = new NodeArray<GreenNode>([leftsquarebracket, stringliteral, rightsquarebracket])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    AlternativeNode? rule_Alternative()
    {
        int __mark = Mark();
        // "|" Molecule+ -Action NewLine
        {
            TokenNode? vertbar;
            NodeArray<MoleculeNode>? moleculePlus;
            ActionNode? action;
            TokenNode? newline;
            if (true
                && (vertbar = Expect(TokenType.VertBar)) is not null
                && (moleculePlus = Repeat(rule_Molecule, 1)) is not null
                && ((action = rule_Action()) is not null || true) // Optional
                && (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return new AlternativeNode(moleculePlus, action)
                {
                    Children = new NodeArray<GreenNode>([vertbar, new NodeArrayWrapNode(moleculePlus), action, newline])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    MoleculeNode? rule_Molecule()
    {
        int __mark = Mark();
        // "&" Atom
        {
            TokenNode? ampersand;
            AtomNode? atom;
            if (true
                && (ampersand = Expect(TokenType.Ampersand)) is not null
                && (atom = rule_Atom()) is not null
            )
            {
                return new LookaheadNode(atom, true)
                {
                    Children = new NodeArray<GreenNode>([ampersand, atom])
                };
            }
        }
        Reset(__mark);
        // "!" Atom
        {
            TokenNode? exclamation;
            AtomNode? atom;
            if (true
                && (exclamation = Expect(TokenType.Exclamation)) is not null
                && (atom = rule_Atom()) is not null
            )
            {
                return new LookaheadNode(atom, false)
                {
                    Children = new NodeArray<GreenNode>([exclamation, atom])
                };
            }
        }
        Reset(__mark);
        // "-" Atom
        {
            TokenNode? minus;
            AtomNode? atom;
            if (true
                && (minus = Expect(TokenType.Minus)) is not null
                && (atom = rule_Atom()) is not null
            )
            {
                return new OptionalNode(atom)
                {
                    Children = new NodeArray<GreenNode>([minus, atom])
                };
            }
        }
        Reset(__mark);
        // Atom "*"
        {
            AtomNode? atom;
            TokenNode? star;
            if (true
                && (atom = rule_Atom()) is not null
                && (star = Expect(TokenType.Star)) is not null
            )
            {
                return new RepeatZeroMoreNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom, star])
                };
            }
        }
        Reset(__mark);
        // Atom "+"
        {
            AtomNode? atom;
            TokenNode? plus;
            if (true
                && (atom = rule_Atom()) is not null
                && (plus = Expect(TokenType.Plus)) is not null
            )
            {
                return new RepeatOneMoreNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom, plus])
                };
            }
        }
        Reset(__mark);
        // Atom
        {
            AtomNode? atom;
            if (true
                && (atom = rule_Atom()) is not null
            )
            {
                return new AtomMoleculeNode(atom)
                {
                    Children = new NodeArray<GreenNode>([atom])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    AtomNode? rule_Atom()
    {
        int __mark = Mark();
        // Name
        {
            TokenNode? name;
            if (true
                && (name = Expect(TokenType.Name)) is not null
            )
            {
                return new NameAtomNode(name.RawString)
                {
                    Children = new NodeArray<GreenNode>([name])
                };
            }
        }
        Reset(__mark);
        // StringLiteral
        {
            TokenNode? stringliteral;
            if (true
                && (stringliteral = Expect(TokenType.StringLiteral)) is not null
            )
            {
                return new StringAtomNode(stringliteral.RawString)
                {
                    Children = new NodeArray<GreenNode>([stringliteral])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    ActionNode? rule_Action()
    {
        int __mark = Mark();
        // "->" StringLiteral
        {
            TokenNode? rightarrow;
            TokenNode? stringliteral;
            if (true
                && (rightarrow = Expect(TokenType.RightArrow)) is not null
                && (stringliteral = Expect(TokenType.StringLiteral)) is not null
            )
            {
                return new ActionNode(StringParser.ParseQuotedString(stringliteral.RawString))
                {
                    Children = new NodeArray<GreenNode>([rightarrow, stringliteral])
                };
            }
        }
        Reset(__mark);
        return null;
    }

    public override GrammarNode? Parse() => rule_Start();
    protected override HashSet<string> Keywords => [];
}
