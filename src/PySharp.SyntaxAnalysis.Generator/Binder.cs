using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class Binder
{
    internal readonly Dictionary<string, BoundRule> Rules = [];
    internal readonly BoundGrammar Grammar = new();

    private readonly VariablesNamingScope groupTypeNameStore = new();
    private readonly Dictionary<NodeArray<AlternativeNode>, BoundRule> groupRules = [];

    internal void ReadMetadata(IEnumerable<MetadataNode> metadata)
    {
        string? userHeader = null, parserName = null;
        foreach (var meta in metadata)
        {
            switch (meta.Name)
            {
                case "header":
                    userHeader = meta.StringValue;
                    break;
                case "parser_name":
                    parserName = meta.StringValue;
                    break;
                default:
                    throw new InvalidNameException($"Unexpected metadata name: {meta.Name}.");
            }
        }

        if (userHeader is null)
            throw new IncompleteMetadataException("header");
        if (parserName is null)
            throw new IncompleteMetadataException("parser_name");

        Grammar.UserHeader = userHeader;
        Grammar.ParserName = parserName;
    }

    internal void RegisterRules(IEnumerable<RuleNode> rules)
    {
        foreach (var astRule in rules)
        {
            var rule = searchGroupsAndCreateTypes(astRule.Alternatives, astRule.RecoverText(), astRule.Name);

            if (astRule.Decorators.Contains("main"))
            {
                if (Grammar.MainRule is not null)
                    throw new CompilationException($"Cannot have two rules marked as main at one time: {Grammar.MainRule.Name}, {rule.Name}");

                Grammar.MainRule = rule;
                Grammar.TopLevelNodeName = rule.TypeName;
            }
        }
    }

    private BoundRule searchGroupsAndCreateTypes(NodeArray<AlternativeNode> alternatives, string sourceText, string? name = null)
    {
        var groups = alternatives
            .SelectMany(a => a.Molecules)
            .SelectMany<MoleculeNode, AtomNode>(m => m switch
            {
                AtomMoleculeNode hydrogen => [hydrogen.Atom],
                LookaheadNode look => [look.Atom],
                OptionalNode opt => [opt.Atom],
                RepeatMoleculeNode rep => [rep.Atom],
                GatherNode gath => [gath.ValueAtom, gath.Separator],
                _ => throw new UnreachableException($"Unexpected MoleculeNode subclass: {m.GetType()}")
            })
            .OfType<GroupAtomNode>();

        foreach (var group in groups)
        {
            searchGroupsAndCreateTypes(group.Alternatives, group.RecoverText());
        }

        bool anonymous = name is null;
        name ??= alternatives[0].Action is NamedActionNode named ? named.Name : null;
        name ??= groupTypeNameStore.NextTypeName();

        if (TokenType.IsReserved(name))
            throw new InvalidNameException($"Cannot create such rule: name '{name}' is reserved for token name.");

        var rule = Rules[name] = new() { Name = name, AstAlternatives = alternatives, SourceText = sourceText };

        if (anonymous)
            groupRules[alternatives] = rule;

        Grammar.Rules.Add(rule);
        return rule;
    }

    internal void PopulateRules()
    {
        if (Rules.Count < 1)
            throw new InvalidOperationException("No registered rules found.");

        foreach (var rule in Rules.Values)
        {
            foreach (var astAlt in rule.AstAlternatives)
            {
                var alt = new BoundAlternative { SourceText = astAlt.RecoverText() };
                foreach (var entry in createEntries(astAlt))
                {
                    alt.Variables[entry.Name] = entry;
                }
                rule.Alternatives.Add(alt);
            }
        }
    }

    private IEnumerable<BoundAlternativeEntry> createEntries(AlternativeNode alternative)
    {
        var nameScope = new VariablesNamingScope();
        foreach (var molecule in alternative.Molecules)
        {
            QuantifierKind quant;
            AtomNode atom;
            int? count = null;
            bool? positive = null;

            switch (molecule)
            {
                case AtomMoleculeNode hydrogen:
                    quant = QuantifierKind.Expect;
                    atom = hydrogen.Atom;
                    break;
                case RepeatMoleculeNode repeat:
                    quant = QuantifierKind.Repeat;
                    atom = repeat.Atom;
                    count = repeat.MinCount;
                    break;
                case LookaheadNode look:
                    quant = QuantifierKind.Lookahead;
                    positive = look.Positiveness;
                    atom = look.Atom;
                    break;
                case OptionalNode opt:
                    quant = QuantifierKind.Optional;
                    atom = opt.Atom;
                    break;
                case GatherNode gather:
                    var localNameScope = new VariablesNamingScope();
                    var value = createEntry(gather.ValueAtom, localNameScope, QuantifierKind.Expect, null, null);
                    var sep = createEntry(gather.Separator, localNameScope, QuantifierKind.Expect, null, null);
                    yield return new BoundGatherAlternativeEntry()
                    {
                        Name = nameScope.NextName(value.Name) + QuantifierKind.Gather.ToString(),
                        Value = value,
                        Separator = sep,
                        Quantifier = QuantifierKind.Gather,
                        MinRepeatCount = null,
                        Positiveness = null,
                    };
                    continue;

                default:
                    throw new UnreachableException($"Unexpected MoleculeNode subclass: '{molecule.GetType()}'");
            }

            yield return createEntry(atom, nameScope, quant, count, positive);
        }
    }

    private BoundAlternativeEntry createEntry(AtomNode atom, VariablesNamingScope nameScope, QuantifierKind quant, int? count, bool? positive)
    => atom switch
    {
        StringAtomNode aliasedToken when TokenType.TryGetDelimiterByString(StringParser.ParseQuotedString(aliasedToken.Value), out var tok) => new BoundTokenAlternativeEntry
        {
            Name = nameScope.NextName(tok.ToString()) + quant.GetSuffix(count),
            Value = tok,
            Quantifier = quant,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        StringAtomNode str => new BoundStringAlternativeEntry()
        {
            Name = nameScope.NextString() + quant.GetSuffix(count),
            Value = str.Parsed,
            Quantifier = quant,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        NameAtomNode name => name.Value switch
        {
            string tokenName when Enum.TryParse<TokenType>(tokenName, out _) => new BoundTokenAlternativeEntry
            {
                Name = nameScope.NextName(tokenName) + quant.GetSuffix(count),
                Value = Enum.Parse<TokenType>(tokenName),
                Quantifier = quant,
                MinRepeatCount = count,
                Positiveness = positive,
            },
            string ruleName => new BoundRuleAlternativeEntry
            {
                Name = nameScope.NextName(ruleName) + quant.GetSuffix(count),
                Value = Rules.GetValueOrDefault(ruleName) ?? throw new InvalidNameException($"Rule '{ruleName}' is not defined."),
                Quantifier = quant,
                MinRepeatCount = count,
                Positiveness = positive,
            }
        },
        GroupAtomNode groupAtom when groupRules.TryGetValue(groupAtom.Alternatives, out var group) => new BoundRuleAlternativeEntry
        {
            Quantifier = quant,
            Name = nameScope.NextName("group") + quant.GetSuffix(count),
            Value = group,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        GroupAtomNode nonExistentGroup when !groupRules.ContainsKey(nonExistentGroup.Alternatives)
            => throw new ArgumentOutOfRangeException($"Registered group rules does not contain such group: {nonExistentGroup}"),
        _ => throw new ArgumentOutOfRangeException($"Unexpected AtomNode subclass: '{atom.GetType()}'"),
    };

    internal void CreateCaptures()
    {
        foreach (var rule in Rules.Values)
        {
            Debug.Assert(rule.Alternatives.Count == rule.AstAlternatives.Count, $"{rule.Name}: {rule.Alternatives.Count}, {rule.AstAlternatives.Count}");
            for (int i = 0; i < rule.Alternatives.Count; i++)
            {
                var astAlt = rule.AstAlternatives[i];
                if (astAlt.Action is null)
                    continue;

                var boundAlt = rule.Alternatives[i];
                List<BoundCapturedVariable> capturedVariables = [];

                foreach (var argument in astAlt.Action.Arguments)
                {
                    if (argument is NameTargetNode name)
                    {
                        if (boundAlt.Variables.TryGetValue(name.Name, out var entry))
                        {
                            var capturedVar = new BoundCapturedVariable
                            {
                                Name = name.Name,
                                Entry = entry,
                            };
                            capturedVariables.Add(capturedVar);
                        }
                        else
                            throw new InvalidNameException($"Name `{name.Name}` does not exists in this context.");
                    }
                    else
                        throw new CompilationException($"Target `{argument.RecoverText()}` is not valid.");
                }

                string? typeHint = null;
                if (astAlt.Action is NamedActionNode namedAction)
                    typeHint = namedAction.Name + "Node";

                typeHint ??= rule.TypeName;

                boundAlt.Action = new BoundAction
                {
                    TypeHint = typeHint,
                    CapturedVariables = capturedVariables,
                };
            }
        }
    }
}
