using System.Collections.Immutable;
using System.Diagnostics;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Generator;

internal class Binder
{
    internal readonly Dictionary<string, BoundRule> Rules = [];
    internal readonly BoundGrammar Grammar = new();
    internal readonly List<CompilationWarning> Warnings = [];

    private readonly HashSet<string> typeNames = [];

    private BinderStage stage = BinderStage.Empty;

    private const string
        meta_header = "header",
        meta_parser_name = "parser_name",
        decor_main = "main",
        decor_union = "union",
        decor_token_union = "inline",
        decor_memo = "memo";

    private readonly VariableNamingScope groupTypeNameStore = new();
    private readonly Dictionary<GroupIdentifier, BoundRule> groupRules = [];

    internal void ReadMetadata(IEnumerable<MetadataView> metadata)
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
                    throw new InvalidNameException($"Unexpected metadata name: {meta.Key}.")
                    {
                        Line = meta.Position2D.Line,
                    };
            }
        }

        if (userHeader is null)
            throw new IncompleteMetadataException(meta_header) { Line = metadata.Last().EndPosition2D.Line };
        if (parserName is null)
            throw new IncompleteMetadataException(meta_parser_name) { Line = metadata.Last().EndPosition2D.Line };

        Grammar.UserHeader = userHeader;
        Grammar.ParserName = parserName;
    }

    internal void RegisterRules(IEnumerable<RuleView> rules)
    {
        Debug.Assert(stage == BinderStage.Empty);

        foreach (var astRule in rules)
        {
            var alternatives = astRule is ArmedRuleView armed
                ? armed.Arms.Select(a => a.Alternative)
                : astRule is SingleAlternativeRuleView single
                    ? [single.Alternative]
                    : throw new UnreachableException($"Unexpected subclass of the RuleNode: {astRule.GetType()}");

            string name = astRule.Name.RawString;

            if (Rules.ContainsKey(name))
                throw new InvalidNameException($"Name {name} was used twice.") { Line = astRule.Name.Position2D.Line };

            if (Enum.TryParse<TokenType>(name, out _))
                throw new InvalidNameException($"Cannot create such rule: name '{name}' is reserved for token types.")
                {
                    Line = astRule.Name.Position2D.Line,
                };

            var decorators = astRule.Decorators.Select(d => d.Value.RawString);

            if (decorators.Contains(decor_union) && decorators.Contains(decor_token_union))
                throw new CompilationException($"Rule cannot be marked as '{decor_union}' and '{decor_token_union}' both in one time.")
                {
                    Line = astRule.Decorators.Position2D.Line,
                };

            var kind = decorators.Contains(decor_union) ? RuleKind.Union
                    : decorators.Contains(decor_token_union) ? RuleKind.TokenUnion
                    : RuleKind.Type;

            BoundType type = kind switch
            {
                RuleKind.Type => new BoundRuleType
                {
                    Name = name,
                    IsAbstract = alternatives.Count() != 1,
                    Base = null,
                },
                RuleKind.Union => new BoundUnionType
                {
                    Name = name,
                },
                RuleKind.TokenUnion => BoundType.TokenNodeType,
                _ => throw new ArgumentOutOfRangeException(),
            };

            var rule = new BoundRule
            {
                Name = name,
                SourceText = astRule.RecoverText(),
                AstAlternatives = alternatives.ToList(),
                Kind = kind,
                Type = type,
                IsGroup = false,
                EnableMemoization = decorators.Contains(decor_memo),
                LineCreated = astRule.Position2D.Line,
            };
            Rules[rule.Name] = rule;
            Grammar.Rules.Add(rule);
            typeNames.Add(rule.Name);

            foreach (var alt in alternatives)
            {
                foreach (var group in getGroups(alt))
                    createGroupRule(group);
            }

            if (decorators.Contains(decor_main))
            {
                if (Grammar.MainRule is not null)
                    throw new CompilationException($"Cannot have two rules marked as main at one time: {Grammar.MainRule.Name}, {rule.Name}")
                    {
                        Line = rule.LineCreated,
                    };

                Grammar.MainRule = rule;
                Grammar.TopLevelNodeName = rule.Type.Name;
                rule.IsEntryPoint = true;
                rule.WasUsed = true;
            }
        }

        if (Grammar.MainRule is null)
            throw new CompilationException("Grammar should contain one rule declared with `@main` decorator.")
            {
                Line = rules.First().Position2D.Line,
            };

        stage = BinderStage.CreatedRules;
    }

    private void createGroupRule(IGroup astGroup)
    {
        string name;
        if (astGroup.Alternatives.Length == 1)
        {
            name = astGroup.Alternatives[0].Action is NamedActionView typeHint
                ? typeHint.Name.RawString
                : groupTypeNameStore.NextTypeName();
        }
        else
        {
            // TODO: Add better way to create names for groups.
            name = groupTypeNameStore.NextTypeName();
        }

        BoundType? type = null;
        bool isInline = false;

        if (astGroup.Decorator is GroupDecoratorView dec)
        {
            if (dec.Value.RawString == decor_token_union)
            {
                if (astGroup.Alternatives.Any(a => a.Action != null))
                    throw new CompilationException("Inline groups cannot contain arms with the actions.")
                    {
                        Line = astGroup.Position2D.Line,
                    };

                type = BoundType.TokenNodeType;
                isInline = true;
            }
            else
            {
                throw new CompilationException($"Unsupported decorator type: '{dec.Value.RawString}'")
                {
                    Line = astGroup.Decorator.Position2D.Line,
                };
            }
        }

        type ??= new BoundRuleType
        {
            Name = name,
            IsAbstract = astGroup.Alternatives.Length != 1,
            Base = null,
        };

        var groupRule = new BoundRule
        {
            Name = isInline ? groupTypeNameStore.NextNamePreserveCase("_TokenInlineGroup") : name,
            SourceText = astGroup.RecoverText(),
            AstAlternatives = astGroup.Alternatives,
            Type = type,
            Kind = isInline ? RuleKind.TokenUnion : RuleKind.Type,
            IsGroup = true,
            EnableMemoization = false, // Groups cannot use memo.
            LineCreated = astGroup.Position2D.Line,
        };

        if (!groupRules.ContainsKey(astGroup.Identifier))
        {
            Rules[groupRule.Name] = groupRule;
            Grammar.Rules.Add(groupRule);
            groupRules[astGroup.Identifier] = groupRule;
        }

        foreach (var group in astGroup.Alternatives.SelectMany(getGroups))
            createGroupRule(group);
    }

    private static IEnumerable<IGroup> getGroups(AlternativeView alternative) => alternative.Molecules
        .Where(m => m is not OptionalGroupView)
        .SelectMany<MoleculeView, AtomView>(m => m switch
        {
            AtomMoleculeView hydrogen => [hydrogen.Atom],
            PositiveLookaheadView pos => [pos.Atom],
            NegativeLookaheadView neg => [neg.Atom],
            OptionalView opt => [opt.Atom],
            RepeatOneMoreView one => [one.Atom],
            RepeatZeroMoreView zero => [zero.Atom],
            GatherView gath => [gath.ValueAtom, gath.Separator],
            CutView => [],
            _ => throw new UnreachableException($"Unexpected MoleculeNode subclass: {m.GetType()}")
        })
        .OfType<IGroup>()
        .Concat(alternative.Molecules.Where(m => m is OptionalGroupView).Cast<OptionalGroupView>());

    internal void PopulateRules()
    {
        Debug.Assert(stage == BinderStage.CreatedRules);

        foreach (var rule in Rules.Values)
        {
            foreach (var astAlt in rule.AstAlternatives)
            {
                var alt = new BoundAlternative
                {
                    SourceText = astAlt.RecoverText(),
                    EntriesText = astAlt.Molecules.RecoverText()
                };

                foreach (var entry in createEntries(astAlt))
                {
                    alt.Entries.Add(entry);
                }

                if (rule.Kind != RuleKind.Type)
                {
                    if (alt.Variables.Count() != 1)
                        throw new InvalidUnionException($"should have exactly one variable entry: '{astAlt.Molecules.RecoverText()}'. Consider using lookahead because they do not produce variables.")
                        {
                            Line = rule.LineCreated,
                        };

                    if (rule.Kind == RuleKind.TokenUnion && alt.Variables.First() is not BoundTokenAlternativeEntry and not BoundStringAlternativeEntry)
                        throw new CompilationException($"Token union rules cannot have non-token entry as variable: '{astAlt.Molecules.RecoverText()}'")
                        {
                            Line = rule.LineCreated,
                        };

                    if (rule.Kind == RuleKind.Union && alt.Variables.First() is not BoundRuleAlternativeEntry)
                        throw new CompilationException($"Union rules cannot have non-rule entry as variable: '{astAlt.Molecules.RecoverText()}'")
                        {
                            Line = rule.LineCreated,
                        };
                }

                rule.Alternatives.Add(alt);
            }
        }

        stage = BinderStage.CreatedEntries;
    }

    private IEnumerable<BoundAlternativeEntry> createEntries(AlternativeView alternative)
    {
        var nameScope = new VariableNamingScope();
        int index = 0;
        foreach (var molecule in alternative.Molecules)
        {
            QuantifierKind quant;
            AtomView atom;
            int? count = null;
            bool? positive = null;

            switch (molecule)
            {
                case AtomMoleculeView hydrogen:
                    quant = QuantifierKind.Expect;
                    atom = hydrogen.Atom;
                    break;

                case RepeatOneMoreView one:
                    quant = QuantifierKind.Repeat;
                    atom = one.Atom;
                    count = 1;
                    break;

                case RepeatZeroMoreView zero:
                    quant = QuantifierKind.Repeat;
                    atom = zero.Atom;
                    count = 0;
                    break;

                case PositiveLookaheadView pos:
                    quant = QuantifierKind.Lookahead;
                    positive = true;
                    atom = pos.Atom;
                    break;

                case NegativeLookaheadView neg:
                    quant = QuantifierKind.Lookahead;
                    positive = false;
                    atom = neg.Atom;
                    break;

                case OptionalView opt:
                    quant = QuantifierKind.Optional;
                    atom = opt.Atom;
                    break;

                case OptionalGroupView optGroup:
                    var rule = groupRules[((IGroup)optGroup).Identifier];
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

                case GatherView gather:
                    var localNameScope = new VariableNamingScope();
                    var value = createEntry(gather.ValueAtom, localNameScope, QuantifierKind.Expect, null, null);
                    var sep = createEntry(gather.Separator, localNameScope, QuantifierKind.Expect, null, null);
                    yield return new BoundGatherAlternativeEntry()
                    {
                        Name = nameScope.NextName(value.Name) + "_" + QuantifierKind.Gather.ToString(),
                        Value = value,
                        Separator = sep,
                        Quantifier = QuantifierKind.Gather,
                        Index = index++,
                        MinRepeatCount = null,
                        Positiveness = null,
                    };
                    continue;

                case CutView:
                    yield return new BoundCutAlternativeEntry();
                    continue;

                default:
                    throw new UnreachableException($"Unexpected MoleculeNode subclass: '{molecule.GetType()}'");
            }

            var entry = createEntry(atom, nameScope, quant, count, positive);
            yield return entry with
            {
                Index = entry.Quantifier != QuantifierKind.Lookahead ? index : -1,
            };

            if (entry.Quantifier != QuantifierKind.Lookahead)
                index++;
        }
    }

    private BoundAlternativeEntry createEntry(AtomView atom, VariableNamingScope nameScope, QuantifierKind quant, int? count, bool? positive)
    => atom switch
    {
        StringAtomView aliasedToken when TokenType.TryGetDelimiterByString(StringParser.ParseQuoted(aliasedToken.Value.RawString), out var tok) => new BoundTokenAlternativeEntry
        {
            Name = nameScope.NextName(tok.ToString()) + quant.AddSuffix(count),
            Value = tok,
            Quantifier = quant,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        StringAtomView str => new BoundStringAlternativeEntry()
        {
            Name = nameScope.NextString() + quant.AddSuffix(count),
            Value = StringParser.ParseQuoted(str.Value.RawString),
            Quantifier = quant,
            MinRepeatCount = count,
            Positiveness = positive,
        },
        NameAtomView name => name.Value.RawString switch
        {
            string tokenName when Enum.TryParse<TokenType>(tokenName, out _) => new BoundTokenAlternativeEntry
            {
                Name = nameScope.NextName(tokenName) + quant.AddSuffix(count),
                Value = Enum.Parse<TokenType>(tokenName),
                Quantifier = quant,
                MinRepeatCount = count,
                Positiveness = positive,
            },
            string ruleName => new BoundRuleAlternativeEntry
            {
                Name = nameScope.NextName(ruleName) + quant.AddSuffix(count),
                Value = Rules.GetValueOrDefault(ruleName) ?? throw new InvalidNameException($"Rule '{ruleName}' is not defined.")
                {
                    Line = atom.Position2D.Line,
                },
                Quantifier = quant,
                MinRepeatCount = count,
                Positiveness = positive,
            }
        },
        GroupAtomView groupAtom => new BoundRuleAlternativeEntry
        {
            Quantifier = quant,
            Name = nameScope.NextName(groupRules[((IGroup)groupAtom).Identifier].Name) + quant.AddSuffix(count),
            Value = groupRules[((IGroup)groupAtom).Identifier],
            MinRepeatCount = count,
            Positiveness = positive,
        },
        _ => throw new ArgumentOutOfRangeException($"Unexpected AtomNode subclass: '{atom.GetType()}'"),
    };

    private void createCaptures()
    {
        foreach (var rule in Rules.Values)
        {
            if (rule.Kind != RuleKind.Type)
            {
                if (rule.AstAlternatives.FirstOrDefault(a => a.Action is not null) is AlternativeView alt)
                    throw new InvalidUnionException($"cannot have actions: '{rule.Name}'")
                    {
                        Line = alt.Position2D.Line,
                    };

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
                                throw new InvalidNameException($"Field name '{fieldName}' used twice.")
                                {
                                    Line = astAlt.Position2D.Line,
                                };

                            fieldNames.Add(fieldName);
                        }
                        else
                            throw new InvalidNameException($"Name `{argument.Variable.RawString}` does not exists in this context.")
                            {
                                Line = astAlt.Position2D.Line,
                            };
                    }
                }
                else // If action is null, use all variables as captured variables.
                {
                    var fieldNameScope = new VariableNamingScope();
                    capturedVariables = boundAlt.Variables
                        .Select(e => new BoundCapturedVariable
                        {
                            FieldName = fieldNameScope.NextNamePreserveCase(getEntryType(e).Name),
                            Entry = e,
                        })
                        .ToList();
                }

                if (astAlt.Action is InferredActionView && rule.Alternatives.Count > 1)
                {
                    throw new CompilationException("Cannot use `new` keyword for rule that have more than 1 arm.")
                    {
                        Line = astAlt.Position2D.Line,
                    };
                }

                BoundRuleType? type = null;
                if (astAlt.Action is NamedActionView namedAction)
                {
                    string name = namedAction.Name.RawString;

                    if (typeNames.Contains(name))
                        throw new InvalidNameException($"Type name {name} used twice in the grammar.")
                        {
                            Line = astAlt.Position2D.Line,
                        };

                    typeNames.Add(name);

                    type = new BoundRuleType
                    {
                        Base = (BoundRuleType)rule.Type,
                        IsAbstract = false,
                        Name = namedAction.Name.RawString,
                    };
                }

                if (astAlt.Action is null && rule.Alternatives.Count > 1)
                {
                    int counter = 0;
                    string name = rule.Type.Name + "_Derived" + counter;
                    while (typeNames.Contains(name))
                    {
                        counter++;
                        name = rule.Type.Name + "_Derived" + counter;
                    }

                    typeNames.Add(name);

                    type = new BoundRuleType
                    {
                        Name = name,
                        Base = (BoundRuleType)rule.Type,
                        IsAbstract = false,
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
                case RuleKind.Type:
                    if (rule.Alternatives.Count == 1)
                    {
                        var fields = rule.Alternatives[0].Action!.CapturedVariables.Select(createField);

                        if (!rule.IsGroup && rule.AstAlternatives[0].Action is NamedActionView action)
                            throw new CompilationException($"Using name for the rule with single arm is not allowed: {rule.Name}")
                            {
                                Line = action.Position2D.Line,
                            };

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

                case RuleKind.Union:
                    foreach (var alt in rule.Alternatives)
                    {
                        var unionMember = ((BoundRuleAlternativeEntry)alt.Variables.First()).Value.Type;
                        var unionType = (BoundUnionType)rule.Type;
                        unionMember.UnionMembership.Add(unionType);
                        unionType.Members.Add(unionMember);
                    }

                    Grammar.Types.Add(rule.Type);

                    break;

                case RuleKind.TokenUnion:
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
            _ => throw new ArgumentOutOfRangeException(variable.Entry.Quantifier.ToString()),
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

    internal void InspectRules()
    {
        foreach (var rule in computeReachableRules(Grammar.MainRule))
        {
            rule.WasUsed = true;
        }

        foreach (var rule in Rules.Values.Where(r => !r.WasUsed))
        {
            Warnings.Add(new CompilationWarning()
            {
                Line = rule.LineCreated,
                Message = $"Rule '{rule.Name}' is created but never used."
            });
        }


        foreach (var rule in Rules.Values)
        {
            // Rules overall quality.
            if (rule.Kind == RuleKind.TokenUnion)
                continue;

            if (rule.GetAllUsedRules().Any(r => r.IsEntryPoint))
            {
                throw new CompilationException("Rules cannot refer to the top-level rules.")
                {
                    Line = rule.LineCreated,
                };
            }

            for (int currentAltIndex = 1; currentAltIndex < rule.Alternatives.Count; currentAltIndex++)
            {
                var currentAlt = rule.Alternatives[currentAltIndex];
                foreach (var previousAlt in rule.Alternatives[0..currentAltIndex])
                {
                    if (currentAlt.StartsWith(previousAlt))
                    {
                        var warn = new CompilationWarning
                        {
                            Message = $"Alternative will never be reached.",
                            Line = currentAlt.LineCreated,
                        };
                        Warnings.Add(warn);
                    }
                }
            }

            var sccHandled = new List<HashSet<BoundRule>>();

            // Process SCCs.
            var sccIterator = searchStronglyConnectedComponents(rule);
            foreach (var scc in sccIterator)
            {
                // Skip already handled SCCs
                if (sccHandled.Any(handled => handled.SetEquals(scc)))
                    continue;

                switch (scc.Count)
                {
                    case 0:
                        throw new UnreachableException("SCC cannot have 0 elements.");

                    case 1:
                        if (rule.GetPotentialLeftRecursive([]).Contains(rule))
                        {
                            rule.IsLeftRecursive = true;

                            if (!rule.EnableMemoization)
                                throw new CompilationException($"Rule '{rule.Name}' is left recursive and should have explicit memoization tag.")
                                {
                                    Line = rule.LineCreated,
                                };
                        }
                        break;

                    default:
                        throw new NotImplementedException("Implement cycle finding algorithm.");
                }

                sccHandled.Add(scc);
            }
        }
    }

    /// <summary>
    /// Search for the strongly connected components (SCC) in the given rules by the
    /// <see cref="BoundRule.GetPotentialLeftRecursive"/> to detect indirect left recursion.
    /// </summary>
    /// <remarks>
    /// For example for these rules:
    /// <code>
    /// Rule1:
    ///     | Rule2 "terminal"
    ///     | "terminal"
    /// Rule2: Rule3 "alsoTerminal"
    /// Rule3:
    ///     | Rule1 "anotherTerminal"
    ///     | Rule0 "anotherTerminal"
    /// Rule0: "hello"
    /// </code>
    /// going to return SCC: <c>{ Rule1, Rule2, Rule3 }</c> and <c>{ Rule0 }</c>
    /// </remarks>
    /// <param name="vertex">Rule to search on left recursion.</param>
    /// <returns>Sets of the rules that creates SCC.</returns>
    private IEnumerable<HashSet<BoundRule>> searchStronglyConnectedComponents(BoundRule vertex)
    {
        var identified = new HashSet<BoundRule>();
        var stack = new List<BoundRule>();
        var index = new Dictionary<BoundRule, int>();
        var boundaries = new Stack<int>();

        IEnumerable<HashSet<BoundRule>> deepFirstSearch(BoundRule vertex)
        {
            index[vertex] = stack.Count;
            stack.Add(vertex);
            boundaries.Push(index[vertex]);

            foreach (var edge in vertex.GetPotentialLeftRecursive(Warnings))
            {
                if (!index.TryGetValue(edge, out int edgeIndex))
                {
                    foreach (var next in deepFirstSearch(edge))
                        yield return next;
                }
                else if (!identified.Contains(edge))
                {
                    while (edgeIndex < boundaries.Peek())
                        boundaries.Pop();
                }
            }

            if (boundaries.Peek() == index[vertex])
            {
                boundaries.Pop();
                HashSet<BoundRule> stronglyConnectedComponents = stack[index[vertex]..].ToHashSet();
                identified.UnionWith(stronglyConnectedComponents);

                stack.RemoveRange(index[vertex], stack.Count - index[vertex]);

                yield return stronglyConnectedComponents;
            }
        }

        foreach (var scc in deepFirstSearch(vertex))
            yield return scc;
    }

    /// <summary>
    /// Recursively computes all rules that can be reached starting from the given <paramref name="vertex"/>.
    /// </summary>
    private static IEnumerable<BoundRule> computeReachableRules(BoundRule vertex, HashSet<BoundRule>? reached = null)
    {
        reached ??= [];

        reached.Add(vertex);

        foreach (var child in vertex.GetAllUsedRules())
        {
            if (reached.Contains(child))
                continue;

            reached.Add(child);

            yield return child;

            foreach (var grandChild in computeReachableRules(child, reached))
            {
                yield return grandChild;
            }
        }
    }
}
