using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Generator.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class Binder
{
    internal readonly Dictionary<string, BoundRule> Rules = [];
    internal readonly BoundGrammar Grammar = new();

    private readonly VariablesNamingScope groupTypeNameStore = new();
    private readonly Dictionary<AlternativeNode, BoundRule> groupRules = [];

    internal void ReadMetadata(IEnumerable<MetadataNode> metadata)
    {
        string? userHeader = null, parserName = null;
        foreach (var meta in metadata)
        {
            switch (meta.Key.RawString)
            {
                case "header":
                    userHeader = StringParser.ParseQuoted(meta.Value.RawString);
                    break;
                case "parser_name":
                    parserName = StringParser.ParseQuoted(meta.Value.RawString);
                    break;
                default:
                    throw new InvalidNameException($"Unexpected metadata name: {meta.Key}.");
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
            var alternatives = astRule is ArmedRuleNode armed
                ? armed.Arms.Select(a => a.Alternative)
                : astRule is SingleAlternativeRuleNode single
                    ? [single.Alternative]
                    : throw new UnreachableException($"Unexpected subclass of the RuleNode: {astRule.GetType()}");

            string name = astRule.Name.RawString;

            if (Enum.TryParse<TokenType>(name, out _))
                throw new InvalidNameException($"Cannot create such rule: name '{name}' is reserved for token types.");

            var rule = new BoundRule
            {
                Name = name,
                SourceText = astRule.RecoverText(),
                AstAlternatives = alternatives.ToList(),
            };
            Rules[rule.Name] = rule;
            Grammar.Rules.Add(rule);

            foreach (var alt in alternatives)
            {
                foreach (var group in getGroups(alt))
                    createGroupRule(group.Alternative);
            }

            if (astRule.Decorators.Select(d => d.Value.RawString).Contains("main"))
            {
                if (Grammar.MainRule is not null)
                    throw new CompilationException($"Cannot have two rules marked as main at one time: {Grammar.MainRule.Name}, {rule.Name}");

                Grammar.MainRule = rule;
                Grammar.TopLevelNodeName = rule.TypeName;
            }
        }
    }

    private void createGroupRule(AlternativeNode alternative)
    {
        string name = alternative.Action is NamedActionNode typeHint
            ? typeHint.Name.RawString
            : groupTypeNameStore.NextTypeName();

        var groupRule = new BoundRule
        {
            Name = name,
            SourceText = alternative.RecoverText(),
            AstAlternatives = [alternative],
        };

        Rules[groupRule.Name] = groupRule;
        Grammar.Rules.Add(groupRule);
        groupRules[alternative] = groupRule;

        foreach (var group in getGroups(alternative))
            createGroupRule(group.Alternative);
    }

    private static IEnumerable<GroupAtomNode> getGroups(AlternativeNode alternative) => alternative.Molecules
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
                    count = repeat is RepeatOneMoreNode ? 1 : 0;
                    break;
                case LookaheadNode look:
                    quant = QuantifierKind.Lookahead;
                    positive = look is PositiveLookaheadNode;
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
        StringAtomNode aliasedToken when TokenType.TryGetDelimiterByString(StringParser.ParseQuoted(aliasedToken.Value.RawString), out var tok) => new BoundTokenAlternativeEntry
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
            Value = StringParser.ParseQuoted(str.Value.RawString),
            Quantifier = quant,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        NameAtomNode name => name.Value.RawString switch
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
        GroupAtomNode groupAtom when groupRules.TryGetValue(groupAtom.Alternative, out var group) => new BoundRuleAlternativeEntry
        {
            Quantifier = quant,
            Name = nameScope.NextName("group") + quant.GetSuffix(count),
            Value = group,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        GroupAtomNode nonExistentGroup when !groupRules.ContainsKey(nonExistentGroup.Alternative)
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

                foreach (var argument in astAlt.Action.ValueArguments)
                {
                    if (boundAlt.Variables.TryGetValue(argument.Variable.RawString, out var entry))
                    {
                        capturedVariables.Add(new BoundCapturedVariable
                        {
                            VariableName = argument.Variable.RawString,
                            FieldName = argument.Field.RawString,
                            Entry = entry,
                        });
                    }
                    else
                        throw new InvalidNameException($"Name `{argument.Variable.RawString}` does not exists in this context.");
                }

                string? typeHint = null;
                if (astAlt.Action is NamedActionNode namedAction)
                    typeHint = namedAction.Name.RawString + "Node";

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
