using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
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
                Type = new BoundType
                {
                    Name = name + "Node",
                    Base = null
                }
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
                Grammar.TopLevelNodeName = rule.Type.Name;
            }
        }

        if (Grammar.MainRule is null)
            throw new CompilationException("Grammar should contain one rule declared with `@main` decorator.");
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
            Type = new BoundType
            {
                Name = name + "Node",
                Base = null,
            }
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
            PositiveLookaheadNode pos => [pos.Atom],
            NegativeLookaheadNode neg => [neg.Atom],
            OptionalNode opt => [opt.Atom],
            RepeatOneMoreNode one => [one.Atom],
            RepeatZeroMoreNode zero => [zero.Atom],
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
        foreach (var (index, molecule) in alternative.Molecules.Index())
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
                case RepeatOneMoreNode one:
                    quant = QuantifierKind.Repeat;
                    atom = one.Atom;
                    count = 1;
                    break;
                case RepeatZeroMoreNode zero:
                    quant = QuantifierKind.Repeat;
                    atom = zero.Atom;
                    count = 0;
                    break;
                case PositiveLookaheadNode pos:
                    quant = QuantifierKind.Lookahead;
                    positive = true;
                    atom = pos.Atom;
                    break;
                case NegativeLookaheadNode neg:
                    quant = QuantifierKind.Lookahead;
                    positive = false;
                    atom = neg.Atom;
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
                        Index = index,
                        MinRepeatCount = null,
                        Positiveness = null,
                    };
                    continue;

                default:
                    throw new UnreachableException($"Unexpected MoleculeNode subclass: '{molecule.GetType()}'");
            }

            var entry = createEntry(atom, nameScope, quant, count, positive);
            entry.Index = index;
            yield return entry;
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

                var boundAlt = rule.Alternatives[i];
                List<BoundCapturedVariable> capturedVariables = [];

                if (astAlt.Action is not null) // Fill captured variables with arguments in action if it non-null.
                {
                    foreach (var argument in astAlt.Action.Arguments)
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
                }
                else // If action is null, use all entries as captured variables.
                {
                    capturedVariables = boundAlt.Entries
                        .Select(e => new BoundCapturedVariable
                        {
                            VariableName = e.Name,
                            FieldName = getEntryType(e).Name,
                            Entry = e,
                        })
                        .ToList();
                }

                if (astAlt.Action is InferredActionNode && rule.Alternatives.Count > 1)
                {
                    throw new CompilationException("Cannot use `new` keyword for rule that have more than 1 arm.");
                }

                BoundType? type = null;
                if (astAlt.Action is NamedActionNode namedAction)
                {
                    type = new BoundType
                    {
                        Base = rule.Type,
                        Name = namedAction.Name.RawString + "Node",
                    };
                }

                type ??= rule.Type;

                boundAlt.Action = new BoundAction
                {
                    Type = type,
                    CapturedVariables = capturedVariables,
                };
            }
        }
    }

    internal void CreateTypes()
    {
        HashSet<BoundField> baseRuleFields = [];
        List<HashSet<BoundField>> fieldsOfAlternatives = [];

        foreach (var rule in Rules.Values)
        {
            if (rule.Alternatives.Count == 1)
            {
                var fields = rule.Alternatives[0].Action.CapturedVariables.Select(createField);

                rule.Type.Fields = fields.ToList();
                Grammar.Types.Add(rule.Type);
                continue;
            }

            bool isEmpty = true;
            baseRuleFields.Clear();
            fieldsOfAlternatives.Clear();

            foreach (var alt in rule.Alternatives)
            {
                var fields = alt.Action.CapturedVariables.Select(createField);

                var setFields = fields.ToHashSet();

                if (isEmpty)
                    baseRuleFields.UnionWith(setFields);
                else
                    baseRuleFields.IntersectWith(setFields);

                isEmpty = false;
                fieldsOfAlternatives.Add(setFields);
            }

            foreach (var fieldsOfAlt in fieldsOfAlternatives)
            {
                fieldsOfAlt.ExceptWith(baseRuleFields);
            }

            Debug.Assert(rule.Alternatives.Count == fieldsOfAlternatives.Count);

            rule.Type.Fields = baseRuleFields.ToList();
            Grammar.Types.Add(rule.Type);

            for (int i = 0; i < rule.Alternatives.Count; i++)
            {
                var alt = rule.Alternatives[i];
                var altFields = fieldsOfAlternatives[i];
                alt.Action.Type.Fields = altFields.ToList();
                Grammar.Types.Add(alt.Action.Type);
            }
        }
    }

    private static BoundField createField(BoundCapturedVariable variable) => new()
    {
        Index = variable.Entry.Index,
        Kind = variable.Entry.Quantifier switch
        {
            QuantifierKind.Expect or QuantifierKind.Optional => FieldKind.Plain,
            QuantifierKind.Repeat => FieldKind.Array,
            QuantifierKind.Gather => FieldKind.Gather,
            _ => throw new ArgumentOutOfRangeException(),
        },
        Type = getEntryType(variable.Entry),
        AccessModifier = AccessModifier.Internal,
        Name = variable.FieldName,
        IsOptional = variable.Entry.Quantifier == QuantifierKind.Optional,
    };

    private static BoundType getEntryType(BoundAlternativeEntry entry)
        => entry is BoundRuleAlternativeEntry ruleEntry
            ? ruleEntry.Value.Type
            : entry is BoundGatherAlternativeEntry gatherEntry
                ? getEntryType(gatherEntry.Value)
                : BoundType.TokenNodeType;

    private static string createFieldNameFromEntry(BoundAlternativeEntry entry, int index)
        => entry is BoundRuleAlternativeEntry ruleEntry
            ? ruleEntry.Value.Type.Name
            : entry is BoundGatherAlternativeEntry gatherEntry
                ? $"Gather{index}{createFieldNameFromEntry(gatherEntry.Value, index)}"
                : $"Token{index}";
}
