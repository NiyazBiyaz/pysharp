using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

class Compiler(GrammarNode grammar)
{
    private readonly GrammarNode astGrammar = grammar;

    private readonly Dictionary<string, IRuleIr> ruleTable = [];

    public GrammarIr Compile()
    {
        fillRuleTableWithNamed();
        linkAlternates();
        // TODO: Anonymous rules (groups).
        // TODO: Type inference for untyped rules (also groups).

        // Extract metadata.
        string header = null!;
        string parseCallReturn = null!;
        string classSignature = null!;
        foreach (var meta in astGrammar.Metadata)
        {
            switch (meta.Name)
            {
                case "header":
                    header = meta.StringValue;
                    break;

                case "parse_call_return":
                    parseCallReturn = meta.StringValue;
                    break;

                case "class_signature":
                    classSignature = meta.StringValue;
                    break;

                default:
                    throw new ArgumentException("Unexpected metadata type.");
            }
        }
        if (header is null)
            throw new IncompleteMetadataException("header");
        if (parseCallReturn is null)
            throw new IncompleteMetadataException("parse_call_return");
        if (classSignature is null)
            throw new IncompleteMetadataException("class_signature");

        return new GrammarIr([.. ruleTable.Values], header, parseCallReturn, classSignature);
    }

    // First pass.
    private void fillRuleTableWithNamed()
    {
        foreach (var rule in astGrammar.Rules)
        {
            IRuleIr ruleIr;
            if (rule.TypeSpec is not null)
                ruleIr = new NamedTypedRuleIr(rule.Name, rule.TypeSpec.TypeName);
            else
                throw new NotImplementedException();

            ruleTable[ruleIr.Name] = ruleIr;
        }
    }

    // Second pass.
    private void linkAlternates()
    {
        foreach (var rule in astGrammar.Rules)
        {
            List<AlternativeIr> alts = [];

            foreach (var alt in rule.Alternatives)
            {
                var symbols = alt.Molecules.Select((mol, i) => mol switch
                {
                    AtomMoleculeNode atom => getSymbolOfAtom(atom.Atom, i),
                    RepeatOneMoreNode rep1 => QuantifiedSymbolIr.CreateRepeat(getSymbolOfAtom(rep1.Atom, i), 1),
                    RepeatZeroMoreNode rep0 => QuantifiedSymbolIr.CreateRepeat(getSymbolOfAtom(rep0.Atom, i), 0),
                    OptionalNode opt => QuantifiedSymbolIr.CreateOptional(getSymbolOfAtom(opt.Atom, i)),
                    LookaheadNode look => QuantifiedSymbolIr.CreateLookahead(getSymbolOfAtom(look.Atom, i), look.Positiveness),
                    _ => throw new UnreachableException("Unexpected molecule node type")
                });

                string sourceText = string.Concat(alt.Molecules.Select(m => m.RecoverText()));
                // TODO: remove direct expression access as string to "PromisedExpression" or something.
                var alternative = new AlternativeIr(sourceText, symbols, alt.Action.Expression);

                alts.Add(alternative);
            }

            ruleTable[rule.Name].Alternatives = alts;
        }
    }

    private ISymbolIr getSymbolOfAtom(AtomNode atom, int mangleSuffix) => atom switch
    {
        NameAtomNode rule when ruleTable.TryGetValue(rule.Value, out var r) => new RuleSymbolIr(r),

        NameAtomNode name => new TokenSymbolIr(name.Value.ToLowerInvariant(), $"TokenType.{name.Value}"),

        StringAtomNode alias when TokenType.TryGetDelimiterByString(StringParser.ParseQuotedString(alias.Value), out var tt)
            => new TokenSymbolIr(tt.ToString().ToLowerInvariant(), $"TokenType.{tt}"),

        StringAtomNode str => new TokenSymbolIr($"__str_tok{mangleSuffix}", str.Value),

        _ => throw new UnreachableException("Unexpected atom node type."),
    };
}
