using System.Diagnostics;
using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class GrammarCompiler(GrammarNode ast)
{
    private readonly GrammarNode ast = ast;

    private readonly Dictionary<string, string> metadataStore = new()
    {
        ["main_rule_name"] = "Start" // TODO: add '@main' decorator to the rules.
    };

    private readonly List<LambdaTypeIr> promisedLambdas = [];
    private int lambdaTypeCount = 0;

    private readonly Dictionary<string, RuleIr> allRules = [];

    public GrammarData Compile()
    {
        readMetadata();
        readRules();
        linkRulesToSymbols();

        var rules = dumpRules();

        return new()
        {
            MetadataFields = metadataStore.AsReadOnly(),
            Rules = rules.ToList(),
            Types = [],
            Keywords = [], // TODO: add keywords reading.
        };
    }

    private IEnumerable<RuleData> dumpRules()
    {
        foreach (var rule in allRules.Values)
        {
            yield return new RuleData()
            {
                Name = rule.Name,
                ReturnName = rule.Type.Name,
                Alternatives = dumpAlternatives(rule).ToList(),
                OriginalText = rule.OriginalText,
            };
        }
    }

    private IEnumerable<AlternativeData> dumpAlternatives(RuleIr rule)
    {
        foreach (var alt in rule.Alternatives)
        {
            yield return new AlternativeData()
            {
                ReturnExpression = alt.ReturnExpression,
                OriginalText = alt.OriginalText,
                HasOptionals = alt.Symbols.Any(static s => s.Kind == SymbolKind.Optional),
                Conditions = dumpConditions(alt).ToList(),
                Variables = alt.Symbols
                    .Where(static s => s.Kind != SymbolKind.LookPositive && s.Kind != SymbolKind.LookNegative)
                    .Select(static s => new VariableData()
                    {
                        IsOptional = s.Kind == SymbolKind.Optional,
                        TypeName = s.Atom.LinkedRule is null ? "TokenNode" : s.Atom.LinkedRule.Type.Name,
                        NeedWrapper = s.Kind == SymbolKind.Repeat0 || s.Kind == SymbolKind.Repeat1,
                        Name = s.Name,
                    })
                    .ToList(),
            };
        }
    }

    private IEnumerable<ConditionData> dumpConditions(AlternativeIr alt)
    {
        foreach (var symbol in alt.Symbols)
        {
            yield return new ConditionData()
            {
                Kind = symbol.Kind switch
                {
                    SymbolKind.Atom when symbol.Atom.IsToken || symbol.Atom.IsString => ConditionKind.Expect,
                    SymbolKind.Atom => ConditionKind.Rule,
                    SymbolKind.Repeat0 or SymbolKind.Repeat1 => ConditionKind.Repeat,
                    SymbolKind.LookPositive or SymbolKind.LookNegative => ConditionKind.Lookahead,
                    SymbolKind.Optional => ConditionKind.Optional,
                    _ => throw new UnreachableException($"Invalid SymbolKind value: {symbol.Kind}."),
                },
                CallData = symbol.Atom.Value,
                IsString = symbol.Atom.IsString,
                IsToken = symbol.Atom.IsToken,
                AssignedVar = symbol.Name,
                MinCount = symbol.Kind switch
                {
                    SymbolKind.Repeat0 => 0,
                    SymbolKind.Repeat1 => 1,
                    _ => null,
                },
                Positive = symbol.Kind switch
                {
                    SymbolKind.LookPositive => true,
                    SymbolKind.LookNegative => false,
                    _ => null,
                }
            };
        }
    }

    private void readRules()
    {
        foreach (var astRule in ast.Rules)
        {
            TypeIr type;
            if (astRule.TypeSpec is null)
                type = createLambdaType();
            else
                type = new TypeIr(astRule.TypeSpec.TypeName);

            if (TokenType.IsReserved(astRule.Name))
                throw new InvalidNameException($"Name '{astRule.Name}' reserved in TokenType.");

            var rule = new RuleIr(astRule.Name, type, astRule.RecoverText());
            allRules.Add(rule.Name, rule);

            // Link with alternatives.
            rule.Alternatives = [.. astRule.Alternatives.Select(static astAlt => createAlternative(astAlt))];
        }
    }

    private static AlternativeIr createAlternative(AlternativeNode astAlt)
    {
        var namesScope = new VariablesNamingScope();
        return new AlternativeIr()
        {
            Symbols = [.. astAlt.Molecules.Select(molecule => createSymbol(molecule, namesScope))],
            OriginalText = string.Join("", astAlt.Molecules.Select(static m => m.RecoverText())),
            ReturnExpression = astAlt.Action!.Expression // TODO: remove it.
        };
    }

    private static SymbolIr createSymbol(MoleculeNode molecule, VariablesNamingScope scope)
    {
        SymbolKind kind;
        AtomIr atom;
        switch (molecule)
        {
            case AtomMoleculeNode a:
                kind = SymbolKind.Atom;
                atom = createAtom(a.Atom, scope);
                break;
            case RepeatMoleculeNode r:
                kind = r.MinCount == 0 ? SymbolKind.Repeat0 : SymbolKind.Repeat1;
                atom = createAtom(r.Atom, scope);
                break;
            case LookaheadNode l:
                kind = l.Positiveness ? SymbolKind.LookPositive : SymbolKind.LookNegative;
                atom = createAtom(l.Atom, scope);
                break;
            case OptionalNode o:
                kind = SymbolKind.Optional;
                atom = createAtom(o.Atom, scope);
                break;

            default:
                throw new UnreachableException("Unexpected MoleculeNode instance subclass.");
        }

        return new SymbolIr()
        {
            Kind = kind,
            Atom = atom,
        };
    }

    private static AtomIr createAtom(AtomNode atom, VariablesNamingScope scope)
    {
        string name, value;
        bool isToken, isStr;
        switch (atom)
        {
            case NameAtomNode n:
                name = scope.NextName(n.Value);
                value = n.Value;
                isToken = TokenType.IsReserved(n.Value);
                isStr = false;
                break;
            case StringAtomNode s:
                if (TokenType.TryGetDelimiterByString(s.Parsed, out var type))
                {
                    value = Enum.GetName(type)!;
                    name = scope.NextName(value);
                    isToken = true;
                    isStr = false;
                    break;
                }
                name = scope.NextString();
                value = s.Value;
                isToken = false;
                isStr = true;
                break;

            default:
                throw new UnreachableException("Unexpected AtomNode instance subclass.");
        }

        return new AtomIr()
        {
            Name = name,
            Value = value,
            IsToken = isToken,
            IsString = isStr
        };
    }

    private LambdaTypeIr createLambdaType()
    {
        string name = $"LambdaType{++lambdaTypeCount}";
        var type = new LambdaTypeIr(name) { IsResolved = false };
        promisedLambdas.Add(type);
        return type;
    }

    private void readMetadata()
    {
        foreach (var meta in ast.Metadata)
            metadataStore[meta.Name] = meta.StringValue;
    }

    private void linkRulesToSymbols()
    {
        IEnumerable<AtomIr> allAtoms = allRules.Values
            .SelectMany(rule => rule.Alternatives)
            .SelectMany(alt => alt.Symbols)
            .Where(sym => !sym.Atom.IsString && !sym.Atom.IsToken)
            .Select(sym => sym.Atom)
            .ToList();

        foreach (var atom in allAtoms)
        {
            if (allRules.TryGetValue(atom.Value, out var rule))
                atom.LinkedRule = rule;

            else
                throw new InvalidNameException($"Name '{atom.Value}' does not exists.");
        }
    }
}
