using System.Diagnostics;
using System.Text;
using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal class CsGenerator
{
    private readonly StringBuilder builder = new();

    private int indentation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(indentation));
            field = value;
        }
    }

    internal string Dump()
    {
        Debug.Assert(indentation == 0);
        return builder.ToString();
    }

    internal void AddGenerativeAction(ActionIr action)
    {
        AddLine($"_res = new {action.TypeName!}()");
        open();
        AddLine("Children = new NodeArray<IGreenNode>([");
        indentation++;
        foreach (var variable in action.Variables)
        {
            string varName = variable.Name;
            if (variable.IsOptional)
                varName = $"{varName} ?? VoidNode.Instance";

            AddLine($"{varName},");
        }
        indentation--;
        AddLine("]),");

        // close(), but with the semicolon.
        indentation--;
        AddLine("};");

        AddLine("goto _Return;");
    }

    internal void AddPassAction(ActionIr action)
    {
        var variable = action.Variables.First();
        AddLine($"_res = ({variable.TypeName}?){variable.Name};");
        AddLine("goto _Return;");
    }

    internal void AddCondition(ConditionIr ir)
    {
        switch (ir.Kind)
        {
            case QuantifierKind.Expect:
                if (ir.Atom.IsString)
                    add(wrapNull($@"{ir.AssignedVar!.Name} = Expect(""{ir.Atom.CallData}"")"));
                else if (ir.Atom.IsToken)
                    add(wrapNull($"{ir.AssignedVar!.Name} = Expect(TokenType.{ir.Atom.CallData})"));
                else
                    add(wrapNull($"{ir.AssignedVar!.Name} = rule_{ir.Atom.CallData}()"));
                break;

            case QuantifierKind.Optional:
                if (ir.Atom.IsString)
                    add(wrapOpt($@"{ir.AssignedVar!.Name} = Expect(""{ir.Atom.CallData}"")"));
                else if (ir.Atom.IsToken)
                    add(wrapOpt($"{ir.AssignedVar!.Name} = Expect(TokenType.{ir.Atom.CallData})"));
                else
                    add(wrapOpt($"{ir.AssignedVar!.Name} = rule_{ir.Atom.CallData}()"));
                break;

            case QuantifierKind.Lookahead:
                add($"_LookaheadHelper_{ir.Identifier}()");
                break;

            case QuantifierKind.Repeat:
                add(wrapNull($"{ir.AssignedVar!.Name} = _RepeatHelper_{ir.Identifier}()"));
                break;

            case QuantifierKind.Gather:
                add(wrapNull($"{ir.AssignedVar!.Name} = _GatherHelper_{ir.Identifier}()"));
                break;

            case QuantifierKind.Cut:
                add("(_cut = true)");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(ir), $"Unexpected ConditionKind: {ir.Kind}");
        }

        static string wrapOpt(ReadOnlySpan<char> value) => $"(({value}) is not null || true) // Optional";
        static string wrapNull(ReadOnlySpan<char> value) => $"({value}) is not null";
    }

    internal void AddAlternative(AlternativeIr ir)
    {
        addClearedComment(ir.SourceText);

        string entriesText = ir.EntriesText.Trim().Replace("\"", "\\\"");

        AddLine($@"base.LogAlternativeEntered(""{entriesText}"");");

        foreach (var varEmit in ir.Variables)
        {
            if (varEmit.IsArray)
                AddLine($"INodeArray<{varEmit.TypeName ?? nameof(GreenNode)}>? {varEmit.Name};");
            else
                AddLine($"IGreenNode? {varEmit.Name};");
        }

        beginLine();
        add("if (");
        AddCondition(ir.Conditions.First());
        if (ir.Conditions.Count() == 1)
        {
            add(")");
            endLine();
        }
        else
        {
            endLine();
            indentation++;
            foreach (var condition in ir.Conditions.Skip(1))
            {
                AddLine("&&");
                beginLine();
                AddCondition(condition);
                endLine();
            }
            indentation--;
            AddLine(")");
        }

        open();

        AddLine($@"base.LogAlternativeSucceed(""{entriesText}"");");

        if (ir.Action.Kind == ActionKind.Generative)
            AddGenerativeAction(ir.Action);

        else
            AddPassAction(ir.Action);

        close();

        AddLine($@"base.LogAlternativeFailed(""{entriesText}"");");

        // Add gather helpers.
        foreach (var gather in ir.Conditions.Where(c => c.Kind == QuantifierKind.Gather))
        {
            Debug.Assert(gather.Separator != null);

            string valGreenNode = gather.Atom.IsUnion ? nameof(IGreenNode) : nameof(GreenNode);
            string sepGreenNode = gather.Separator.IsUnion ? nameof(IGreenNode) : nameof(GreenNode);

            string greenNode = nameof(GreenNode);

            addLines($$"""
            NodeArray<GreenNode>? _GatherHelper_{{gather.Identifier}}()
            {
                {{valGreenNode}}? _node = {{gather.Atom.Usage}};
                {{sepGreenNode}}? _separator;
                if (_node == null) return null;
                global::System.Collections.Generic.List<GreenNode> _gathered = [({{greenNode}})_node];
                while (true)
                {
                    int _mark = base.Mark();
                    _separator = {{gather.Separator.Usage}};
                    if (_separator == null) break;
                    _node = {{gather.Atom.Usage}};
                    if (_node == null)
                    {
                        base.Reset(_mark);
                        break;
                    }
                    _gathered.Add(({{greenNode}})_separator);
                    _gathered.Add(({{greenNode}})_node);
                }
                return [.. _gathered];
            }
            """);
        }

        // Add repeat helpers.
        foreach (var repeat in ir.Conditions.Where(c => c.Kind == QuantifierKind.Repeat))
        {
            Debug.Assert(repeat.MinCount != null);

            string resultWhenFirstIsNull = repeat.MinCount switch
            {
                0 => "[]",
                1 => "null",
                _ => throw new ArgumentOutOfRangeException(nameof(repeat.MinCount)),
            };

            string typeName = repeat.AssignedVar!.TypeName!;

            addLines($$"""
            NodeArray<{{typeName}}>? _RepeatHelper_{{repeat.Identifier}}()
            {
                {{typeName}}? _node = {{repeat.Atom.Usage}};
                if (_node == null) return {{resultWhenFirstIsNull}};
                global::System.Collections.Generic.List<{{typeName}}> _result = [_node];
                while ((_node = {{repeat.Atom.Usage}}) != null)
                {
                    _result.Add(_node);
                }
                return [.. _result];
            }
            """);
        }

        // Add lookahead helpers.
        foreach (var lookahead in ir.Conditions.Where(c => c.Kind == QuantifierKind.Lookahead))
        {
            Debug.Assert(lookahead.Positiveness != null);

            // .NET decided to convert it to the 'True' and 'False'.
            string positivenessString = lookahead.Positiveness.Value ? "true" : "false";

            addLines($$"""
            bool _LookaheadHelper_{{lookahead.Identifier}}()
            {
                int _mark = base.Mark();
                bool _wasParsed = {{lookahead.Atom.Usage}} != null;
                base.Reset(_mark);
                return _wasParsed == {{positivenessString}};
            }
            """);
        }
    }

    internal void AddRule(RuleIr ir)
    {
        AddRuleHeader(ir);
        AddRuleBody(ir);
        AddRuleEnd(ir);
    }

    internal void AddRuleHeader(RuleIr ir)
    {
        AddLine($"#region {ir.Name}");

        if (ir.IsMemoEnabled)
        {
            AddLine($"private readonly IMemoContainer<{ir.ReturnTypeName}> _memo_{ir.Name} = CreateContainer<{ir.ReturnTypeName}>();");
        }

        if (ir.IsLeftRecursive)
        {
            addLeftRecursionWrapper(ir);
        }

        addClearedComment(ir.SourceText);

        string rawPrefix = ir.IsLeftRecursive ? "raw_" : "";

        AddLine($"{ir.ReturnTypeName}? {rawPrefix}rule_{ir.Name}()");
    }

    private void addLeftRecursionWrapper(RuleIr ir)
    {
        AddLine($"{ir.ReturnTypeName}? rule_{ir.Name}()");

        open();

        addLines($$"""
        base.LogIncreaseLevel();
        base.LogLeftRecursionRuleEntered("{{ir.Name}}");
        {{ir.ReturnTypeName}}? _res = null;
        int _mark = base.Mark();
        int _lastMark = base.Mark();
        if (_memo_{{ir.Name}}.TryGetCache(_mark, out var _memoized))
        {
            base.LogRuleMemoUsed("{{ir.Name}}", _mark, _memoized);
            base.LogDecreaseLevel();
            base.Reset(_memoized.EndPosition);
            return _memoized.Cache;
        }
        base.LogStartGrow("{{ir.Name}}");
        while (true)
        {
            _memo_{{ir.Name}}.UpdateCache(_mark, base.Mark(), _res);
            base.Reset(_mark);
            base.LogNextGrow("{{ir.Name}}");
            var _rawResult = raw_rule_{{ir.Name}}();
            if (_rawResult == null || base.Mark() <= _lastMark)
            {
                break;
            }
            _lastMark = base.Mark();
            _res = _rawResult;
        }
        base.Reset(_lastMark);
        base.LogEndGrow("{{ir.Name}}", _res == null);
        base.LogDecreaseLevel();
        return _res;
        """);

        close();
    }

    internal void AddRuleBody(RuleIr ir)
    {
        open();

        AddLine("base.LogIncreaseLevel();");

        AddLine($@"base.LogRuleEntered(""{ir.Name}"");");

        AddLine("int _mark = base.Mark();");

        if (ir.IsMemoEnabled && !ir.IsLeftRecursive)
        {
            addLines($$"""
            if (_memo_{{ir.Name}}.TryGetCache(_mark, out var _memoized))
            {
                base.LogRuleMemoUsed("{{ir.Name}}", _mark, _memoized);
                base.LogDecreaseLevel();
                base.Reset(_memoized.EndPosition);
                return _memoized.Cache;
            }
            """);
        }

        AddLine($"{ir.ReturnTypeName}? _res = null;");

        if (ir.Alternatives.Any(a => a.HasCut))
            AddLine("bool _cut = false;");

        foreach (var alt in ir.Alternatives)
        {
            open();

            AddAlternative(alt);

            close();

            AddLine("base.Reset(_mark);");
            if (alt.HasCut)
            {
                addLines("""
                if (_cut)
                {
                    _res = null;
                    goto _Return;
                }
                """);
            }
        }

        AddLine($@"base.LogRuleFailed(""{ir.Name}"");");

        indentation--;
        if (ir.IsMemoEnabled && !ir.IsLeftRecursive)
        {
            addLines($"""
            _Return:
                base.LogRuleMemoCreated("{ir.Name}", _mark, _res == null);
                base.LogRuleExiting("{ir.Name}");
                base.LogDecreaseLevel();
                _memo_{ir.Name}.AddCache(_mark, base.Mark(), _res);
                return _res;
            """);
        }
        else
        {
            addLines($"""
            _Return:
                base.LogRuleExiting("{ir.Name}");
                base.LogDecreaseLevel();
                return _res;
            """);
        }
        indentation++;

        close();
    }

    internal void AddRuleEnd(RuleIr ruleIr) => AddLine($"#endregion // {ruleIr.Name}");

    internal void AddParserSignature(AccessModifier accessModifier, string parserName, string topLevelNodeName)
    {
        string modifierName = accessModifier.CodeRepresentation();

        AddLine($"{modifierName} partial class {parserName}(ITokenNodeStream _tokenStream) : BaseParser<{topLevelNodeName}>(_tokenStream)");
    }

    internal void AddParserBody(string mainName, string mainTypeName, IEnumerable<RuleIr> ruleIrs, IEnumerable<string> keywords)
    {
        open();

        beginLine();
        add("protected override HashSet<string> Keywords => [");
        if (keywords.Any())
        {
            endLine();
            indentation++;
            foreach (var kwd in keywords)
            {
                AddLine($"\"{kwd}\",");
            }
            indentation--;
            AddLine("];");
        }
        else
        {
            add("];");
            endLine();
        }

        addBlankLine();

        AddLine($"public override {mainTypeName}? Parse() => rule_{mainName}();");

        foreach (var rule in ruleIrs)
        {
            addBlankLine();

            AddRule(rule);
        }

        close();
    }

    internal void AddFileHeader(string userHeader, string grammarName)
    {
        addLines("""
        // <auto-generated/>
        // Bau bau!

        #nullable enable
        """);
        addBlankLine();
        AddLine($"// Generated from '{grammarName}'");
        addLines(userHeader);
        addBlankLine();
    }

    internal void AddFileBody(string grammarEmit) => addLines(grammarEmit);

    internal void AddTypes(IEnumerable<TypeIr> typeIrs)
    {
        if (!typeIrs.Any())
            return;

        bool addBlank = false;

        foreach (var type in typeIrs)
        {
            if (addBlank)
                addBlankLine();
            addBlank = true;

            if (type.Kind == TypeKind.Rule)
            {
                AddTypeSignature(type);
                AddTypeBody(type);
            }
            else
            {
                AddUnion(type);
            }
        }
    }

    internal void AddTypeBody(TypeIr ir)
    {
        open();

        foreach (var field in ir.Fields)
        {
            string modifier = field.AccessModifier.CodeRepresentation();

            switch (field.Kind)
            {
                case FieldKind.Plain:
                    if (field.IsOptional)
                    {
                        AddLine($"{modifier} {field.TypeName}? {field.Name} => Children![{field.ChildIndex}] as {field.TypeName};");
                    }
                    else
                    {
                        AddLine($"{modifier} {field.TypeName} {field.Name} => ({field.TypeName})Children![{field.ChildIndex}];");
                    }
                    break;

                case FieldKind.Array:
                    AddLine($"""
                    {modifier} NodeArray<{field.TypeName}> {field.Name} => (NodeArray<{field.TypeName}>)Children![{field.ChildIndex}];
                    """);
                    break;

                case FieldKind.Gather:
                    addLines($$"""
                    private global::System.Collections.Immutable.ImmutableArray<{{field.TypeName}}>? _field_{{field.Name}} = null;
                    {{modifier}} global::System.Collections.Immutable.ImmutableArray<{{field.TypeName}}> {{field.Name}}
                    {
                        get
                        {
                            if (_field_{{field.Name}} is null)
                            {
                                var _tmp = Ast{{field.Name}}.Where(static (_, i) => i % 2 == 0).Cast<{{field.TypeName}}>();
                                _field_{{field.Name}} = global::System.Collections.Immutable.ImmutableArray.ToImmutableArray(_tmp);
                            }
                            return _field_{{field.Name}}.Value;
                        }
                    }
                    {{modifier}} NodeArray<GreenNode> Ast{{field.Name}} => (NodeArray<GreenNode>)Children![{{field.ChildIndex}}];
                    """);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        close();
    }

    internal void AddTypeSignature(TypeIr ir)
    {
        string modifierName = ir.AccessModifier.CodeRepresentation();

        beginLine();
        add($"{modifierName} {(ir.IsAbstract!.Value ? "abstract" : "sealed")} partial record {ir.Name} : {ir.BaseName}");
        foreach (string union in ir.UnionMembership)
        {
            Debug.Assert(union != null);
            add($", {union}");
        }
        endLine();
    }

    internal void AddUnion(TypeIr ir)
    {
        string modifierName = ir.AccessModifier.CodeRepresentation();

        beginLine();
        add($"{modifierName} partial interface {ir.Name} : IGreenNode");
        foreach (var union in ir.UnionMembership)
            add($", {union}");

        add(";");
        endLine();
    }

    private const string indent_string = "    ";
    private const string new_line = "\n";

    internal void AddLine(ReadOnlySpan<char> value)
    {
        beginLine();
        add(value);
        endLine();
    }

    private void open()
    {
        AddLine("{");
        indentation++;
    }

    private void close()
    {
        indentation--;
        AddLine("}");
    }

    private void addBlankLine() => endLine();

    private void beginLine()
    {
        for (int i = 0; i < indentation; i++)
            add(indent_string);
    }

    private void endLine() => add(new_line);

    private void addLines(ReadOnlySpan<char> value)
    {
        foreach (var line in value.Trim().EnumerateLines())
        {
            AddLine(line);
        }
    }

    private void add(ReadOnlySpan<char> value) => builder.Append(value);

    private void addClearedComment(ReadOnlySpan<char> value)
    {
        bool wasNonComment = false;

        foreach (var line in value.Trim().EnumerateLines())
        {
            if (wasNonComment)
            {
                beginLine();
                add("//");
            }

            int current = 0;
            while (current < line.Length && char.IsWhiteSpace(line[current]))
                current++;

            if (current == line.Length || line[current] == '#')
            {
                if (wasNonComment)
                    endLine();

                continue;
            }

            if (!wasNonComment)
            {
                beginLine();
                add("//");
            }

            wasNonComment = true;

            add(" ");
            add(line[..current]);

            // Now it will stop on whatever char that '#' and even in strings, but it's okay for Python.
            while (current < line.Length && line[current] != '#')
            {
                add(line.Slice(current, 1));
                current++;
            }

            endLine();
        }
    }
}
