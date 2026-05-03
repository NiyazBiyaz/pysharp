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
using PySharp.SyntaxAnalysis.Tokens;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal class GrammarParser(ITokenNodeStream tokenStream) : BaseParser<GrammarNode>(tokenStream)
{
    GrammarNode? rule_Start()
    {
        int mark = Mark();
        {
            NodeArray<MetadataNode>? metas;
            NodeArray<RuleNode>? rules;
            TokenNode? endoffile;
            if (true
                && (metas = rule_Metas()) is not null
                && (rules = rule_Rules()) is not null
                && (endoffile = Expect(TokenType.EndOfFile)) is not null
            )
            {
                return new GrammarNode(metas, rules)
                {
                    Children = new NodeArray<GreenNode>([new NodeArrayWrapNode(metas), new NodeArrayWrapNode(rules), endoffile])
                };
            }
        }
        Reset(mark);
        return null;
    }

    NodeArray<MetadataNode>? rule_Metas()
    {
        int mark = Mark();
        {
            MetadataNode? meta;
            NodeArray<MetadataNode>? metas;
            if (true
                && (meta = rule_Meta()) is not null
                && (metas = rule_Metas()) is not null
            )
            {
                return new([meta, .. metas]);
            }
        }
        Reset(mark);
        {
            MetadataNode? meta;
            if (true
                && (meta = rule_Meta()) is not null
            )
            {
                return new([meta]);
            }
        }
        Reset(mark);
        return null;
    }

    MetadataNode? rule_Meta()
    {
        int mark = Mark();
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
        Reset(mark);
        return null;
    }

    NodeArray<RuleNode>? rule_Rules()
    {
        int mark = Mark();
        {
            RuleNode? rule;
            NodeArray<RuleNode>? rules;
            if (true
                && (rule = rule_Rule()) is not null
                && (rules = rule_Rules()) is not null
            )
            {
                return new([rule, .. rules]);
            }
        }
        Reset(mark);
        {
            RuleNode? rule;
            if (true
                && (rule = rule_Rule()) is not null
            )
            {
                return new([rule]);
            }
        }
        Reset(mark);
        return null;
    }

    RuleNode? rule_Rule()
    {
        int mark = Mark();
        {
            TokenNode? name;
            TypeSpecNode? typespec;
            TokenNode? colon;
            TokenNode? newline;
            TokenNode? indent;
            NodeArray<AlternativeNode>? alternatives;
            TokenNode? dedent;
            if (true
                && (name = Expect(TokenType.Name)) is not null
                && (typespec = rule_TypeSpec()) is not null
                && (colon = Expect(TokenType.Colon)) is not null
                && (newline = Expect(TokenType.NewLine)) is not null
                && (indent = Expect(TokenType.Indent)) is not null
                && (alternatives = rule_Alternatives()) is not null
                && (dedent = Expect(TokenType.Dedent)) is not null
            )
            {
                return new RuleNode(name.RawString, typespec, alternatives)
                {
                    Children = new NodeArray<GreenNode>([name, typespec, colon, newline, indent, new NodeArrayWrapNode(alternatives), dedent])
                };
            }
        }
        Reset(mark);
        return null;
    }

    TypeSpecNode? rule_TypeSpec()
    {
        int mark = Mark();
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
        Reset(mark);
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
        Reset(mark);
        return null;
    }

    NodeArray<AlternativeNode>? rule_Alternatives()
    {
        int mark = Mark();
        {
            AlternativeNode? alternative;
            NodeArray<AlternativeNode>? alternatives;
            if (true
                && (alternative = rule_Alternative()) is not null
                && (alternatives = rule_Alternatives()) is not null
            )
            {
                return new([alternative, .. alternatives]);
            }
        }
        Reset(mark);
        {
            AlternativeNode? alternative;
            if (true
                && (alternative = rule_Alternative()) is not null
            )
            {
                return new([alternative]);
            }
        }
        Reset(mark);
        return null;
    }

    AlternativeNode? rule_Alternative()
    {
        int mark = Mark();
        {
            TokenNode? vertbar;
            NodeArray<AtomNode>? atoms;
            ActionNode? action;
            TokenNode? newline;
            if (true
                && (vertbar = Expect(TokenType.VertBar)) is not null
                && (atoms = rule_Atoms()) is not null
                && (action = rule_Action()) is not null
                && (newline = Expect(TokenType.NewLine)) is not null
            )
            {
                return new AlternativeNode(atoms, action)
                {
                    Children = new NodeArray<GreenNode>([vertbar, new NodeArrayWrapNode(atoms), action, newline])
                };
            }
        }
        Reset(mark);
        return null;
    }

    NodeArray<AtomNode>? rule_Atoms()
    {
        int mark = Mark();
        {
            AtomNode? atom;
            NodeArray<AtomNode>? atoms;
            if (true
                && (atom = rule_Atom()) is not null
                && (atoms = rule_Atoms()) is not null
            )
            {
                return new([atom, .. atoms]);
            }
        }
        Reset(mark);
        {
            AtomNode? atom;
            if (true
                && (atom = rule_Atom()) is not null
            )
            {
                return new([atom]);
            }
        }
        Reset(mark);
        return null;
    }

    AtomNode? rule_Atom()
    {
        int mark = Mark();
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
        Reset(mark);
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
        Reset(mark);
        return null;
    }

    ActionNode? rule_Action()
    {
        int mark = Mark();
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
        Reset(mark);
        return null;
    }

    public override GrammarNode? Parse() => rule_Start();
    protected override HashSet<string> Keywords => [];
}
