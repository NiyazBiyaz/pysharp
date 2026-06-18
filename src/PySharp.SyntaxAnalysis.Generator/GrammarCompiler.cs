using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class GrammarCompiler(GrammarNode ast)
{
    private readonly GrammarNode ast = ast;

    private readonly Dictionary<string, string> metadataStore = [];

    private readonly Dictionary<string, RuleIr> allRules = [];

    private readonly List<TypeIr> anonymousTypes = [];
    private readonly VariablesNamingScope anonymousTypesNames = new();

    private readonly Dictionary<GroupAtomNode, RuleIr> anonRuleCache = [];

    public GrammarData Compile()
    {
        readMetadata();
        readRules();
        linkRulesToSymbols();

        var rules = dumpRules();
        var types = dumpTypes();

        return new()
        {
            MetadataFields = metadataStore.AsReadOnly(),
            Rules = rules.ToList(),
            Types = types.ToList(),
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
                IsUnion = rule.IsUnion,
                IsAnonymous = rule.IsAnonymous,
            };
        }
    }

    private IEnumerable<AlternativeData> dumpAlternatives(RuleIr rule)
    {
        foreach (var alt in rule.Alternatives)
        {
            yield return new AlternativeData()
            {
                ReturnExpression = alt.ReturnExpression switch
                {
                    not null => alt.ReturnExpression,
                    null when rule.IsUnion =>
                        alt.Symbols.First(s => s.Kind != SymbolKind.LookPositive && s.Kind != SymbolKind.LookNegative).Name,
                    null when rule.IsAnonymous => rule.Type.Name,
                    _ => throw new UnreachableException("Unexpected condition while ReturnExpression processing."),
                },
                OriginalText = alt.OriginalText,
                HasOptionals = alt.Symbols.Any(static s => s.Kind == SymbolKind.Optional),
                Conditions = dumpConditions(alt).ToList(),
                Variables = dumpVariables(alt),
            };
        }
    }

    private IEnumerable<ConditionData> dumpConditions(AlternativeIr alt)
    {
        foreach (var symbol in alt.Symbols)
        {
            ConditionKind kind;
            yield return new ConditionData()
            {
                Kind = kind = symbol.Kind switch
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
                AssignedVar = kind != ConditionKind.Lookahead ? symbol.Name : "",
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
            if (TokenType.IsReserved(astRule.Name))
                throw new InvalidNameException($"Name '{astRule.Name}' reserved in TokenType.");

            var typeName = astRule.TypeSpec is null ? nameof(GreenNode) : astRule.TypeSpec.TypeName;

            var rule = registerRule(astRule.Name, typeName, astRule.RecoverText(), astRule.Alternatives);

            // Process decorators.
            if (astRule is DecoratedRuleNode decorated)
            {
                if (decorated.Decorator == "main")
                    metadataStore["main_rule_name"] = rule.Name;
                else if (decorated.Decorator == "union")
                    rule.IsUnion = true;
            }
        }
    }

    private RuleIr registerRule(string ruleName, string typeName, string sourceText, IEnumerable<AlternativeNode> alts)
    {
        var type = new TypeIr(typeName);
        var rule = new RuleIr(ruleName, type, sourceText)
        {
            Alternatives = [.. alts.Select(createAlternative)],
        };
        allRules.Add(rule.Name, rule);
        return rule;
    }

    private AlternativeIr createAlternative(AlternativeNode astAlt)
    {
        var namesScope = new VariablesNamingScope();
        return new AlternativeIr()
        {
            Symbols = [.. astAlt.Molecules.Select(molecule => createSymbol(molecule, namesScope))],
            OriginalText = string.Join("", astAlt.Molecules.Select(static m => m.RecoverText())),
            ReturnExpression = astAlt.Action?.Expression,
        };
    }

    private SymbolIr createSymbol(MoleculeNode molecule, VariablesNamingScope scope)
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

    private AtomIr createAtom(AtomNode atom, VariablesNamingScope scope)
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
                // Check that string can be replaced with token type.
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
            case GroupAtomNode g:
                if (!anonRuleCache.TryGetValue(g, out var rule))
                {
                    var newType = registerAnonType();
                    rule = registerRule(newType.Name, newType.Name, g.RecoverText(), g.Alternatives);
                    rule.IsAnonymous = true;
                    anonRuleCache[g] = rule;
                    newType.Rule = rule;
                }
                value = rule.Name;
                name = scope.NextName("group");
                isToken = false;
                isStr = false;
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

    private TypeIr registerAnonType()
    {
        string name = anonymousTypesNames.NextTypeName();
        var type = new TypeIr(name);
        anonymousTypes.Add(type);
        return type;
    }

    private IEnumerable<TypeData> dumpTypes()
    {
        foreach (var type in anonymousTypes)
        {
            if (type.Rule is null)
                throw new UnreachableException("Anonymous types should always have reference to rule.");

            if (type.Rule.Alternatives.Count > 1)
                throw new NotImplementedException("Only one alternative supported now.");

            var alt = type.Rule.Alternatives.First();

            yield return new()
            {
                Name = type.Name,
                Fields = dumpVariables(alt),
                AccessModifier = TypeAccessModifier.Anonymous,
            };
        }
    }

    private List<VariableData> dumpVariables(AlternativeIr alt) => alt.Symbols
        .Where(static s => s.Kind != SymbolKind.LookPositive && s.Kind != SymbolKind.LookNegative)
        .Select(static s => new VariableData()
        {
            IsOptional = s.Kind == SymbolKind.Optional,
            TypeName = s.Atom.LinkedRule is null ? "TokenNode" : s.Atom.LinkedRule.Type.Name,
            NeedWrapper = s.Kind == SymbolKind.Repeat0 || s.Kind == SymbolKind.Repeat1,
            Name = s.Name,
        })
        .ToList();

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
