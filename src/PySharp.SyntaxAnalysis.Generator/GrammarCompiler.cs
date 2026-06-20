using System.Data;
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

        return new(metadataStore.AsReadOnly(), rules.ToList(), types.ToList(), [/*TODO*/]);
    }

    private IEnumerable<RuleData> dumpRules()
    {
        foreach (var rule in allRules.Values)
        {
            yield return new RuleData(
                rule.Name,
                rule.Type.Name,
                dumpAlternatives(rule).ToList(),
                rule.OriginalText,
                rule.IsUnion,
                rule.IsAnonymous
            );
        }
    }

    private IEnumerable<AlternativeData> dumpAlternatives(RuleIr rule)
    {
        foreach (var alt in rule.Alternatives)
        {
            List<VariableData> captureVariables;
            yield return new AlternativeData(
                alt.OriginalText,
                captureVariables = dumpVariables(alt),
                dumpConditions(alt).ToList(),
                alt.Symbols.Any(static s => s.Kind == SymbolKind.Optional),
                alt.Action?.ConstructibleType.Name ?? rule.Type.Name,
                alt.Action is not null // If action is not specified, probably it is a anonymous type.
                    ? dumpTargets(alt.Action.Targets).ToList()
                    : convertVariablesToArgs(captureVariables).ToList()
            );
        }
    }

    private IEnumerable<CtorArgumentData> convertVariablesToArgs(List<VariableData> captureVariables)
    {
        foreach (var var in captureVariables)
        {
            yield return new CtorArgumentData(CtorArgumentType.Raw, var.Name);
        }
    }

    private IEnumerable<CtorArgumentData> dumpTargets(List<TargetIr> targets)
    {
        foreach (var tar in targets)
        {
            CtorArgumentData data;
            if (tar.IsBoolConst)
            {
                data = new CtorArgumentData(CtorArgumentType.BoolConstant, null, BoolConstant: tar.BoolConstValue);
            }
            else if (tar.IsArrayWrapper)
            {
                data = new CtorArgumentData(CtorArgumentType.WrapArray, tar.Symbol!.Name);
            }
            else
            {
                data = new CtorArgumentData((tar.IsGroupAxis, tar.IsString, tar.IsParseString) switch
                {
                    (true, false, false) => CtorArgumentType.GroupAxis,
                    (true, true, false) => CtorArgumentType.GroupAxisString,
                    (true, true, true) => CtorArgumentType.GroupAxisParseString,
                    (false, true, false) => CtorArgumentType.String,
                    (false, true, true) => CtorArgumentType.ParseString,
                    (false, false, false) => CtorArgumentType.Raw,
                    _ => throw new UnreachableException($"Unexpected target flags condition."),
                }, tar.Symbol!.Name, tar.AxisName);
            }
            yield return data;
        }
    }
    private IEnumerable<ConditionData> dumpConditions(AlternativeIr alt)
    {
        foreach (var sym in alt.Symbols)
        {
            ConditionKind kind;
            yield return new ConditionData()
            {
                Kind = kind = sym.Kind switch
                {
                    SymbolKind.Atom when sym.Atom.IsToken || sym.Atom.IsString => ConditionKind.Expect,
                    SymbolKind.Atom => ConditionKind.Rule,
                    SymbolKind.Repeat0 or SymbolKind.Repeat1 => ConditionKind.Repeat,
                    SymbolKind.LookPositive or SymbolKind.LookNegative => ConditionKind.Lookahead,
                    SymbolKind.Optional => ConditionKind.Optional,
                    SymbolKind.Gather => ConditionKind.Gather,
                    _ => throw new UnreachableException($"Invalid SymbolKind value: {sym.Kind}."),
                },
                Atom = new(sym.Atom),
                Separator = sym is GatherSymbolIr gather ? new(gather.Separator) : null,
                AssignedVar = kind != ConditionKind.Lookahead ? sym.Name : "",
                MinCount = sym.Kind switch
                {
                    SymbolKind.Repeat0 => 0,
                    SymbolKind.Repeat1 => 1,
                    _ => null,
                },
                Positive = sym.Kind switch
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
            foreach (var decorator in astRule.Decorators)
            {
                if (decorator == "main")
                    metadataStore["main_rule_name"] = rule.Name;
                else if (decorator == "union")
                    rule.IsUnion = true;
            }
        }
    }

    private RuleIr registerRule(string ruleName, string typeName, string sourceText, IEnumerable<AlternativeNode> alts)
    {
        var type = new TypeIr(typeName);
        var rule = new RuleIr(ruleName, type, sourceText)
        {
            Alternatives = [.. alts.Select(a => createAlternative(a, type))]
        };
        allRules.Add(rule.Name, rule);
        return rule;
    }

    private AlternativeIr createAlternative(AlternativeNode astAlt, TypeIr type)
    {
        ActionIr? action = null;

        var namesScope = new VariablesNamingScope();

        Dictionary<string, SymbolIr> symbols = [];
        foreach (var molecule in astAlt.Molecules)
        {
            var symbol = createSymbol(molecule, namesScope);
            if (symbol.Kind != SymbolKind.LookPositive && symbol.Kind != SymbolKind.LookNegative)
                symbols[symbol.Name] = symbol;
        }

        if (astAlt.Action is not null)
        {
            if (astAlt.Action is NamedActionNode named)
                type = new(named.Name);

            List<TargetIr> targets = [];
            foreach (var arg in astAlt.Action.Arguments)
            {
                var _arg = arg;
                bool isStr = false, isParse = false;

                if (arg is StringTargetNode str)
                {
                    _arg = str.TokenTarget;
                    isStr = true;
                }
                else if (arg is ParseStringTargetNode parse)
                {
                    _arg = parse.TokenTarget;
                    isStr = isParse = true;
                }

                TargetIr tar;
                switch (_arg)
                {
                    case NameTargetNode n:
                    {
                        if (!symbols.TryGetValue(n.Name, out var symbol))
                            throw new UndeclaredUsageUserException("alternative symbols", n.Name);

                        tar = new(symbol)
                        {
                            IsString = isStr,
                            IsParseString = isParse,
                        };
                        break;
                    }
                    case GroupAxisTargetNode g:
                    {
                        if (!symbols.TryGetValue(g.Name, out var symbol))
                            throw new UndeclaredUsageUserException("alternative symbols", g.Name);

                        tar = new(symbol)
                        {
                            AxisName = g.AxisName,
                            IsGroupAxis = true,
                            IsString = isStr,
                            IsParseString = isParse,
                        };
                        break;
                    }
                    case ToArrayTargetNode ta:
                    {
                        if (!symbols.TryGetValue(ta.Name, out var symbol))
                            throw new UndeclaredUsageUserException("alternative symbols", ta.Name);

                        tar = new(symbol)
                        {
                            IsArrayWrapper = true,
                        };
                        break;
                    }
                    case BoolConstTargetNode b:
                    {
                        tar = new(null)
                        {
                            IsBoolConst = true,
                            BoolConstValue = b.Value,
                        };
                        break;
                    }
                    case StringTargetNode:
                    case ParseStringTargetNode:
                        throw new UnreachableException("Such targets should be replaced with inner targets.");
                    default:
                        throw new UnreachableException("Unexpected TargetNode instance subclass.");
                }

                targets.Add(tar);
            }

            action = new ActionIr(type, targets);
        }

        return new AlternativeIr(
            string.Join("", astAlt.Molecules.Select(static m => m.RecoverText())),
            symbols.Values.ToList(),
            action
        );
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
            case GatherNode g:
                return new GatherSymbolIr
                {
                    Kind = SymbolKind.Gather,
                    Atom = createAtom(g.ValueAtom, scope),
                    Separator = createAtom(g.Separator, scope),
                };

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

        return new AtomIr(name, value, isToken, isStr);
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

            yield return new(type.Name, TypeAccessModifier.Anonymous, dumpVariables(alt));
        }
    }

    private List<VariableData> dumpVariables(AlternativeIr alt) => alt.Symbols
        .Where(static s => s.Kind != SymbolKind.LookPositive && s.Kind != SymbolKind.LookNegative)
        .Select(static s => new VariableData(
            s.Name,
            s.Atom.LinkedRule is null ? "TokenNode" : s.Atom.LinkedRule.Type.Name,
            s.Kind == SymbolKind.Repeat0 || s.Kind == SymbolKind.Repeat1 || s.Kind == SymbolKind.Gather,
            s.Kind == SymbolKind.Optional
        ))
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
