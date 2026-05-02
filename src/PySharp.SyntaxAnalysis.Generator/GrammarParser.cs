using PySharp.SyntaxAnalysis.Tokens;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

public class GrammarParser(ITokenNodeStream tokenStream) : BaseParser<GrammarNode>(tokenStream)
{
    protected override HashSet<string> Keywords => [];

    public override GrammarNode? Start()
    {
        var metadata = ruleMetas();
        if (metadata is null)
            return null;

        var rules = ruleRules();
        if (rules is null)
            return null;

        var eof = Expect(TokenType.EndOfFile);
        if (eof == null)
            return null;


        var rulesWrap = new NodeArrayWrapNode(rules);
        var metadataWrap = new NodeArrayWrapNode(metadata);

        return new(metadata, rules)
        {
            Children = new NodeArray<GreenNode>([metadataWrap, rulesWrap, eof])
        };
    }

    private NodeArray<MetadataNode>? ruleMetas()
    {
        int mark = Mark();

        MetadataNode? meta;
        NodeArray<MetadataNode>? metas;
        if ((meta = ruleMeta()) is not null && (metas = ruleMetas()) is not null)
        {
            return new([.. metas, meta]);
        }

        Reset(mark);

        meta = ruleMeta();

        if (meta != null)
            return new([meta]);

        Reset(mark);

        return null;
    }

    private MetadataNode? ruleMeta()
    {
        int mark = Mark();

        TokenNode? at;
        TokenNode? name;
        TokenNode? stringVal;
        TokenNode? newLine;
        if ((at = Expect(TokenType.At)) is not null
         && (name = Expect(TokenType.Name)) is not null
         && (stringVal = Expect(TokenType.StringLiteral)) is not null
         && (newLine = Expect(TokenType.NewLine)) is not null)
        {
            // Strings in name cannot be null.
            return new(name.RawString!, StringParser.ParseQuotedString(stringVal.RawString))
            {
                Children = new NodeArray<GreenNode>([at, name, stringVal, newLine])
            };
        }

        Reset(mark);

        return null;
    }

    private NodeArray<RuleNode>? ruleRules()
    {
        int mark = Mark();

        RuleNode? rule;
        NodeArray<RuleNode>? rules;
        if ((rule = ruleRule()) is not null && (rules = ruleRules()) is not null)
        {
            return new([.. rules, rule]);
        }

        Reset(mark);

        rule = ruleRule();

        if (rule != null)
            return new([rule]);

        Reset(mark);

        return null;
    }

    private RuleNode? ruleRule()
    {
        int mark = Mark();

        TokenNode? name;
        TokenNode? colon;
        TokenNode? newLine;
        TokenNode? indent;
        NodeArray<AlternativeNode>? alternatives;
        TokenNode? dedent;

        if ((name = Expect(TokenType.Name)) is not null
         && (colon = Expect(TokenType.Colon)) is not null
         && (newLine = Expect(TokenType.NewLine)) is not null
         && (indent = Expect(TokenType.Indent)) is not null
         && (alternatives = ruleAlternatives()) is not null
         && (dedent = Expect(TokenType.Dedent)) is not null)
        {
            return new(name.RawString, alternatives)
            {
                Children = new NodeArray<GreenNode>([
                    name, colon, newLine, indent, new NodeArrayWrapNode(alternatives), dedent
                ]),
            };
        }

        Reset(mark);

        return null;
    }

    private NodeArray<AlternativeNode>? ruleAlternatives()
    {
        int mark = Mark();

        AlternativeNode? alt;
        NodeArray<AlternativeNode>? alts;
        if ((alt = ruleAlternative()) is not null
         && (alts = ruleAlternatives()) is not null)
        {
            return new([.. alts, alt]);
        }

        Reset(mark);

        alt = ruleAlternative();

        if (alt != null)
            return new([alt]);

        Reset(mark);

        return null;
    }

    private AlternativeNode? ruleAlternative()
    {
        int mark = Mark();

        TokenNode? vertBar;
        NodeArray<AtomNode>? atoms;
        ActionNode? action;
        TokenNode? newLine;

        if ((vertBar = Expect(TokenType.VertBar)) is not null
         && (atoms = ruleAtoms()) is not null
         && (action = ruleAction()) is not null
         && (newLine = Expect(TokenType.NewLine)) is not null)
        {
            return new(atoms, action)
            {
                Children = new NodeArray<GreenNode>([vertBar, new NodeArrayWrapNode(atoms), action, newLine])
            };
        }

        Reset(mark);

        if ((vertBar = Expect(TokenType.VertBar)) is not null
         && (atoms = ruleAtoms()) is not null
         && (newLine = Expect(TokenType.NewLine)) is not null)
        {
            return new(atoms, null)
            {
                Children = new NodeArray<GreenNode>([vertBar, new NodeArrayWrapNode(atoms), newLine])
            };
        }

        Reset(mark);

        return null;
    }

    private NodeArray<AtomNode>? ruleAtoms()
    {
        int mark = Mark();

        AtomNode? atom;
        NodeArray<AtomNode>? atoms;
        if ((atom = ruleAtom()) is not null && (atoms = ruleAtoms()) is not null)
        {
            return new([.. atoms, atom]);
        }

        Reset(mark);

        atom = ruleAtom();

        if (atom != null)
            return new([atom]);

        Reset(mark);

        return null;
    }

    private AtomNode? ruleAtom()
    {
        int mark = Mark();

        TokenNode? token;
        if ((token = Expect(TokenType.Name)) is not null)
        {
            // Name token string is never null.
            return new NameAtomNode(token.RawString!)
            {
                Children = new NodeArray<GreenNode>([token])
            };
        }

        Reset(mark);

        if ((token = Expect(TokenType.StringLiteral)) is not null)
        {
            // StringLiteral token string is never null.
            return new StringAtomNode(token.RawString!)
            {
                Children = new NodeArray<GreenNode>([token])
            };
        }

        Reset(mark);

        return null;
    }

    private ActionNode? ruleAction()
    {
        int mark = Mark();

        TokenNode? arrow;
        TokenNode? strExpr;
        if ((arrow = Expect(TokenType.RightArrow)) is not null
         && (strExpr = Expect(TokenType.StringLiteral)) is not null)
        {
            return new(StringParser.ParseQuotedString(strExpr.RawString))
            {
                Children = new NodeArray<GreenNode>([arrow, strExpr])
            };
        }

        Reset(mark);

        return null;
    }
}
