using System.Collections.Immutable;
using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class Binder
{
    internal readonly Dictionary<string, BoundRule> Rules = [];
    internal readonly BoundGrammar Grammar = new();

    private BinderStage stage = BinderStage.Empty;

    private const string
        meta_header = "header",
        meta_parser_name = "parser_name",
        decor_main = "main",
        decor_union = "union",
        decor_token_union = "inline";

    private readonly VariableNamingScope groupTypeNameStore = new();
    // TODO: now it's too messy to create identity by the ast. Would be better to replace it with the some more deterministic.
    private readonly Dictionary<NodeArray<GreenNode>, BoundRule> groupRules = [];

    internal void ReadMetadata(IEnumerable<MetadataNode> metadata)
    {
        string? userHeader = null, parserName = null;
        foreach (var meta in metadata)
        {
            switch (meta.Key.RawString)
            {
                case meta_header:
                    userHeader = StringParser.ParseQuoted(meta.Value.RawString);
                    break;
                case meta_parser_name:
                    parserName = StringParser.ParseQuoted(meta.Value.RawString);
                    break;
                default:
                    throw new InvalidNameException($"Unexpected metadata name: {meta.Key}.");
            }
        }

        if (userHeader is null)
            throw new IncompleteMetadataException(meta_header);
        if (parserName is null)
            throw new IncompleteMetadataException(meta_parser_name);

        Grammar.UserHeader = userHeader;
        Grammar.ParserName = parserName;
    }

    internal void RegisterRules(IEnumerable<RuleNode> rules)
    {
        Debug.Assert(stage == BinderStage.Empty);

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

            var decorators = astRule.Decorators.Select(d => d.Value.RawString);

            if (decorators.Contains(decor_union) && decorators.Contains(decor_token_union))
                throw new CompilationException($"Rule cannot be marked as '{decor_union}' and '{decor_token_union}' both in one time.");

            var kind = decorators.Contains(decor_union) ? BoundRuleKind.Union
                    : decorators.Contains(decor_token_union) ? BoundRuleKind.TokenUnion
                    : BoundRuleKind.Type;

            BoundType type = kind switch
            {
                BoundRuleKind.Type => new BoundRuleType
                {
                    Name = name + "Node",
                    Base = null,
                },
                BoundRuleKind.Union => new BoundUnionType
                {
                    Name = "I" + name + "Node",
                },
                BoundRuleKind.TokenUnion => BoundType.TokenNodeType,
                _ => throw new ArgumentOutOfRangeException(),
            };

            var rule = new BoundRule
            {
                Name = name,
                SourceText = astRule.RecoverText(),
                AstAlternatives = alternatives.ToList(),
                Kind = kind,
                Type = type,
            };
            Rules[rule.Name] = rule;
            Grammar.Rules.Add(rule);

            foreach (var alt in alternatives)
            {
                foreach (var group in getGroups(alt))
                    createGroupRule(group);
            }

            if (decorators.Contains(decor_main))
            {
                if (Grammar.MainRule is not null)
                    throw new CompilationException($"Cannot have two rules marked as main at one time: {Grammar.MainRule.Name}, {rule.Name}");

                Grammar.MainRule = rule;
                Grammar.TopLevelNodeName = rule.Type.Name;
            }
        }

        if (Grammar.MainRule is null)
            throw new CompilationException("Grammar should contain one rule declared with `@main` decorator.");

        stage = BinderStage.CreatedRules;
    }

    private void createGroupRule(IGroup astGroup)
    {
        string name;
        if (astGroup.Alternatives.Length == 1)
        {
            name = astGroup.Alternatives[0].Action is NamedActionNode typeHint
                ? typeHint.Name.RawString
                : groupTypeNameStore.NextTypeName();
        }
        else
        {
            // TODO: Add better way to create names for groups.
            name = groupTypeNameStore.NextTypeName();
        }

        var groupRule = new BoundRule
        {
            Name = name,
            SourceText = astGroup.AstAlternatives.RecoverText(),
            AstAlternatives = astGroup.Alternatives,
            Type = new BoundRuleType
            {
                Name = name + "Node",
                Base = null,
            },
            Kind = BoundRuleKind.Type, // TODO: Maybe add some decorators to groups syntax too?
        };

        if (!groupRules.ContainsKey(astGroup.AstAlternatives))
        {
            Rules[groupRule.Name] = groupRule;
            Grammar.Rules.Add(groupRule);
            groupRules[astGroup.AstAlternatives] = groupRule;
        }

        foreach (var group in astGroup.Alternatives.SelectMany(getGroups))
            createGroupRule(group);
    }

    private static IEnumerable<IGroup> getGroups(AlternativeNode alternative) => alternative.Molecules
        .Where(m => m is not OptionalGroupNode) // Add it later.
        .SelectMany<MoleculeNode, AtomNode>(m => m switch
        {
            AtomMoleculeNode hydrogen => [hydrogen.Atom],
            PositiveLookaheadNode pos => [pos.Atom],
            NegativeLookaheadNode neg => [neg.Atom],
            OptionalNode opt => [opt.Atom],
            RepeatOneMoreNode one => [one.Atom],
            RepeatZeroMoreNode zero => [zero.Atom],
            GatherNode gath => [gath.ValueAtom, gath.Separator],
            CutNode => [],
            _ => throw new UnreachableException($"Unexpected MoleculeNode subclass: {m.GetType()}")
        })
        .OfType<IGroup>()
        .Concat(alternative.Molecules.Where(m => m is OptionalGroupNode).Cast<OptionalGroupNode>());

    internal void PopulateRules()
    {
        Debug.Assert(stage == BinderStage.CreatedRules);

        if (Rules.Count < 1)
            throw new InvalidOperationException("No registered rules found.");

        foreach (var rule in Rules.Values)
        {
            foreach (var astAlt in rule.AstAlternatives)
            {
                var alt = new BoundAlternative { SourceText = astAlt.RecoverText() };

                foreach (var entry in createEntries(astAlt))
                {
                    alt.Entries.Add(entry);

                }

                if (rule.Kind != BoundRuleKind.Type)
                {
                    if (alt.Variables.Count() != 1)
                        throw new InvalidUnionException($"should have exactly one variable entry: '{astAlt.Molecules.RecoverText()}'. Consider using lookahead because they do not produce variables.");

                    if (rule.Kind == BoundRuleKind.TokenUnion && alt.Variables.First() is not BoundTokenAlternativeEntry and not BoundStringAlternativeEntry)
                        throw new CompilationException($"Token union rules cannot have non-token entry as variable: '{astAlt.Molecules.RecoverText()}'");

                    if (rule.Kind == BoundRuleKind.Union && alt.Variables.First() is not BoundRuleAlternativeEntry)
                        throw new CompilationException($"Union rules cannot have non-rule entry as variable: '{astAlt.Molecules.RecoverText()}'");
                }

                rule.Alternatives.Add(alt);
            }
        }

        stage = BinderStage.CreatedEntries;
    }

    private IEnumerable<BoundAlternativeEntry> createEntries(AlternativeNode alternative)
    {
        var nameScope = new VariableNamingScope();
        int index = 0;
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

                case OptionalGroupNode optGroup:
                    var rule = groupRules[optGroup.AstAlternatives];
                    yield return new BoundRuleAlternativeEntry
                    {
                        Name = nameScope.NextName(rule.Name),
                        Value = rule,
                        Quantifier = QuantifierKind.Optional,
                        Index = index++,
                        MinRepeatCount = null,
                        Positiveness = null,
                    };
                    continue;

                case GatherNode gather:
                    var localNameScope = new VariableNamingScope();
                    var value = createEntry(gather.ValueAtom, localNameScope, QuantifierKind.Expect, null, null);
                    var sep = createEntry(gather.Separator, localNameScope, QuantifierKind.Expect, null, null);
                    yield return new BoundGatherAlternativeEntry()
                    {
                        Name = nameScope.NextName(value.Name) + QuantifierKind.Gather.ToString(),
                        Value = value,
                        Separator = sep,
                        Quantifier = QuantifierKind.Gather,
                        Index = index++,
                        MinRepeatCount = null,
                        Positiveness = null,
                    };
                    continue;

                case CutNode:
                    yield return new BoundCutAlternativeEntry();
                    continue;

                default:
                    throw new UnreachableException($"Unexpected MoleculeNode subclass: '{molecule.GetType()}'");
            }

            var entry = createEntry(atom, nameScope, quant, count, positive);
            entry.Index = entry.Quantifier != QuantifierKind.Lookahead ? index : -1;
            yield return entry;

            if (entry.Quantifier != QuantifierKind.Lookahead)
                index++;
        }
    }

    private BoundAlternativeEntry createEntry(AtomNode atom, VariableNamingScope nameScope, QuantifierKind quant, int? count, bool? positive)
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
        GroupAtomNode groupAtom => new BoundRuleAlternativeEntry
        {
            Quantifier = quant,
            Name = nameScope.NextName(groupRules[groupAtom.AstAlternatives].Name) + quant.GetSuffix(count),
            Value = groupRules[groupAtom.AstAlternatives],
            MinRepeatCount = count,
            Positiveness = positive,
        },
        _ => throw new ArgumentOutOfRangeException($"Unexpected AtomNode subclass: '{atom.GetType()}'"),
    };

    private void createCaptures()
    {
        foreach (var rule in Rules.Values)
        {
            if (rule.Kind != BoundRuleKind.Type)
            {
                if (rule.Alternatives.Any(a => a.Action is not null))
                    throw new InvalidUnionException($"cannot have actions: '{rule.Name}'");

                continue;
            }

            // To count already added fields and prevent using multiple fields with same name.
            var fieldNames = new HashSet<string>();

            Debug.Assert(rule.Alternatives.Count == rule.AstAlternatives.Count, $"{rule.Name}: {rule.Alternatives.Count}, {rule.AstAlternatives.Count}");

            for (int i = 0; i < rule.Alternatives.Count; i++)
            {
                var astAlt = rule.AstAlternatives[i];

                var boundAlt = rule.Alternatives[i];
                List<BoundCapturedVariable> capturedVariables = [];

                if (astAlt.Action is not null) // Fill captured variables with arguments in action if it non-null.
                {
                    fieldNames.Clear();
                    foreach (var argument in astAlt.Action.Arguments?.Value ?? [])
                    {
                        var entry = boundAlt.Variables.FirstOrDefault(v => v.Name == argument.Variable.RawString);
                        if (entry is not null)
                        {
                            string fieldName;
                            capturedVariables.Add(new BoundCapturedVariable
                            {
                                FieldName = fieldName = argument.Field.RawString,
                                Entry = entry,
                            });

                            if (fieldNames.Contains(fieldName))
                                throw new InvalidNameException($"Field name '{fieldName}' used twice.");

                            fieldNames.Add(fieldName);
                        }
                        else
                            throw new InvalidNameException($"Name `{argument.Variable.RawString}` does not exists in this context.");
                    }
                }
                else // If action is null, use all entries as captured variables.
                {
                    var fieldNameScope = new VariableNamingScope();
                    capturedVariables = boundAlt.Entries
                    .Select(e => new BoundCapturedVariable
                    {
                        FieldName = fieldNameScope.NextNamePreserveCase(getEntryType(e).Name),
                        Entry = e,
                    })
                    .ToList();
                }

                if (astAlt.Action is InferredActionNode && rule.Alternatives.Count > 1)
                {
                    throw new CompilationException("Cannot use `new` keyword for rule that have more than 1 arm.");
                }

                BoundRuleType? type = null;
                if (astAlt.Action is NamedActionNode namedAction)
                {
                    type = new BoundRuleType
                    {
                        Base = (BoundRuleType)rule.Type,
                        Name = namedAction.Name.RawString + "Node",
                    };
                }

                type ??= (BoundRuleType)rule.Type;

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
        Debug.Assert(stage == BinderStage.CreatedEntries);

        createCaptures();

        HashSet<BoundField> baseRuleFields = [];
        List<HashSet<BoundField>> fieldsOfAlternatives = [];

        foreach (var rule in Rules.Values)
        {
            switch (rule.Kind)
            {
                case BoundRuleKind.Type:
                    if (rule.Alternatives.Count == 1)
                    {
                        var fields = rule.Alternatives[0].Action!.CapturedVariables.Select(createField);

                        ((BoundRuleType)rule.Type).Fields = fields.ToList();
                        Grammar.Types.Add(rule.Type);
                        continue;
                    }

                    bool isEmpty = true;
                    baseRuleFields.Clear();
                    fieldsOfAlternatives.Clear();

                    foreach (var alt in rule.Alternatives)
                    {
                        var fields = alt.Action!.CapturedVariables.Select(createField);

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

                    ((BoundRuleType)rule.Type).Fields = baseRuleFields.ToList();
                    Grammar.Types.Add(rule.Type);

                    for (int i = 0; i < rule.Alternatives.Count; i++)
                    {
                        var alt = rule.Alternatives[i];
                        var altFields = fieldsOfAlternatives[i];
                        alt.Action!.Type.Fields = altFields.ToList();
                        Grammar.Types.Add(alt.Action.Type);
                    }
                    break;

                case BoundRuleKind.Union:
                    foreach (var alt in rule.Alternatives)
                    {
                        var unionMember = ((BoundRuleAlternativeEntry)alt.Variables.First()).Value.Type;
                        var unionType = (BoundUnionType)rule.Type;
                        unionMember.UnionMembership.Add(unionType);
                        unionType.Members.Add(unionMember);
                    }

                    Grammar.Types.Add(rule.Type);

                    break;

                case BoundRuleKind.TokenUnion:
                    break;
            }
        }

        stage = BinderStage.CreatedTypes;
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
