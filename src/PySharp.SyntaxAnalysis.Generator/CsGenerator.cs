using System.Diagnostics;
using System.Text;
using Humanizer;
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
        string typeName = nodeName(action.TypeName!);
        AddLine($"_res = new {typeName}()");
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
        string typeName = nodeName(variable.TypeName!, variable.TypeIsUnion);
        AddLine($"_res = ({typeName}?){variable.Name};");
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

        foreach (var varIr in ir.Variables)
        {
            string typeName = nodeName(varIr.TypeName ?? "Green", varIr.TypeIsUnion);

            if (varIr.IsArray)
                AddLine($"INodeArray<{typeName}>? {varIr.Name};");
            else
                AddLine($"IGreenNode? {varIr.Name};");
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

            string typeName = nodeName(repeat.AssignedVar!.TypeName!, repeat.AssignedVar!.TypeIsUnion);

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
        string returnTypeName = ir.Kind switch
        {
            RuleKind.Type => nodeName(ir.Name, false),
            RuleKind.Union => nodeName(ir.Name, true),
            RuleKind.TokenUnion => "TokenNode",
            _ => throw new ArgumentOutOfRangeException(nameof(ir.Kind), ir.Kind.ToString()),
        };

        AddRuleHeader(ir, returnTypeName);
        AddRuleBody(ir, returnTypeName);
        AddRuleEnd(ir);
    }

    internal void AddRuleHeader(RuleIr ir, string typeName)
    {
        AddLine($"#region {ir.Name}");

        if (ir.IsMemoEnabled)
        {
            AddLine($"private readonly IMemoContainer<{typeName}> _memo_{ir.Name} = CreateContainer<{typeName}>();");
        }

        if (ir.IsLeftRecursive)
        {
            addLeftRecursionWrapper(ir, typeName);
        }

        addClearedComment(ir.SourceText);

        string rawPrefix = ir.IsLeftRecursive ? "raw_" : "";

        AddLine($"{typeName}? {rawPrefix}rule_{ir.Name}()");
    }

    private void addLeftRecursionWrapper(RuleIr ir, string typeName)
    {
        AddLine($"{typeName}? rule_{ir.Name}()");

        open();

        addLines($$"""
        base.LogIncreaseLevel();
        base.LogLeftRecursionRuleEntered("{{ir.Name}}");
        {{typeName}}? _res = null;
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

    internal void AddRuleBody(RuleIr ir, string typeName)
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

        AddLine($"{typeName}? _res = null;");

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

        AddLine($"{modifierName} partial class {parserName}(ITokenNodeStream _tokenStream) : BaseParser<{topLevelNodeName}Node>(_tokenStream)");
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

        AddLine($"public override {mainTypeName}Node? Parse() => rule_{mainName}();");

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

            if (type.Kind == TypeKind.Node)
            {
                AddNodeType(type);
            }
            else
            {
                AddUnionType(type);
            }
        }
    }

    internal void AddNodeType(TypeIr ir)
    {
        string modifierName = ir.AccessModifier.CodeRepresentation();
        string abstractOrSealed = ir.IsAbstract!.Value ? "abstract" : "sealed";
        string typeNodeName = nodeName(ir.Name);
        string baseNodeName = nodeName(ir.BaseName ?? "Green");

        // Add node class.
        beginLine();
        add($"{modifierName} {abstractOrSealed} partial record {typeNodeName} : {baseNodeName}");
        foreach (string union in ir.UnionMembership)
        {
            Debug.Assert(union != null);
            string unionName = nodeName(union, true);
            add($", {unionName}");
        }
        endLine();

        string typeViewName = viewName(ir.Name, ir.Kind == TypeKind.Union);

        open();

        foreach (var field in ir.Fields)
        {
            string modifier = field.AccessModifier.CodeRepresentation();
            string fieldTypeName = nodeName(field.TypeName, field.TypeIsUnion);

            switch (field.Kind)
            {
                case FieldKind.Plain:
                    if (field.IsOptional)
                    {
                        AddLine($"{modifier} {fieldTypeName}? {field.Name} => Children![{field.ChildIndex}] as {fieldTypeName};");
                    }
                    else
                    {
                        AddLine($"{modifier} {fieldTypeName} {field.Name} => ({fieldTypeName})Children![{field.ChildIndex}];");
                    }
                    break;

                case FieldKind.Array:
                    AddLine($"""
                    {modifier} NodeArray<{fieldTypeName}> {field.Name} => (NodeArray<{fieldTypeName}>)Children![{field.ChildIndex}];
                    """);
                    break;

                case FieldKind.Gather:
                    addLines($$"""
                    private global::System.Collections.Immutable.ImmutableArray<{{fieldTypeName}}>? _field_{{field.Name}} = null;
                    {{modifier}} global::System.Collections.Immutable.ImmutableArray<{{fieldTypeName}}> {{field.Name}}
                    {
                        get
                        {
                            if (_field_{{field.Name}} is null)
                            {
                                var _tmp = Ast{{field.Name}}.Where(static (_, i) => i % 2 == 0).Cast<{{fieldTypeName}}>();
                                _field_{{field.Name}} = global::System.Collections.Immutable.ImmutableArray.ToImmutableArray(_tmp);
                            }
                            return _field_{{field.Name}}.Value;
                        }
                    }
                    {{modifier}} NodeArray<GreenNode> Ast{{field.Name}} => (NodeArray<GreenNode>)Children![{{field.ChildIndex}}];
                    """);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(ir.Kind), ir.Kind.ToString());
            }
        }

        if (!ir.IsAbstract.Value)
        {
            AddLine($"public override {typeViewName} GetView(TokenPosition position, IRedView? parent)");
            AddLine($"    => new {typeViewName}(this, position, parent);");
        }

        close();

        // Add view class.
        string baseViewName = viewName(ir.BaseName ?? "Red");

        beginLine();
        add($"{modifierName} {abstractOrSealed} partial class {typeViewName} : {baseViewName}");
        foreach (string union in ir.UnionMembership)
        {
            Debug.Assert(union != null);
            string unionName = viewName(union, true);
            add($", {unionName}");
        }
        endLine();

        open();

        addLines($$"""
        {{modifierName}} {{typeViewName}}({{typeNodeName}} green, TokenPosition position, IRedView? parent)
            : base(green, position, parent)
        {
        }
        """);

        foreach (var field in ir.Fields)
        {
            addBlankLine();

            string modifier = field.AccessModifier.CodeRepresentation();
            string fieldTypeName = viewName(field.TypeName, field.TypeIsUnion);
            string backingFieldName = "_field_" + field.Name.Camelize();

            string? action = null;
            string greenField = $"(({typeNodeName})base.Green).{field.Name}";
            switch (field.Kind)
            {
                case FieldKind.Plain:
                    action = $"{greenField}!.GetView(_positionOfField, this)";
                    break;

                case FieldKind.Array:
                    action = $"new ViewArray<{fieldTypeName}>({greenField}, _positionOfField, this)";
                    fieldTypeName = $"ViewArray<{fieldTypeName}>";
                    break;

                case FieldKind.Gather:
                    greenField = $"(({typeNodeName})base.Green).Ast{field.Name}";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(field.Kind), field.Kind.ToString());
            }

            string optional = field.IsOptional ? "?" : "";
            string optionalCondition = field.IsOptional ? $" && {greenField} != null" : "";

            if (field.Kind != FieldKind.Gather)
            {
                Debug.Assert(action != null);

                addLines($$"""
                private {{fieldTypeName}}? {{backingFieldName}} = null;
                {{modifier}} {{fieldTypeName}}{{optional}} {{field.Name}}
                {
                    get
                    {
                        if ({{backingFieldName}} == null{{optionalCondition}})
                        {
                            var _positionOfField = base.GetPositionFor({{field.ChildIndex}});
                            {{backingFieldName}} = ({{fieldTypeName}}){{action}};
                        }
                        return ({{fieldTypeName}}{{optional}}){{backingFieldName}};
                    }
                }
                """);
            }
            else
            {
                addLines($$"""
                private ViewArray<RedView>? _ast{{backingFieldName}} = null;
                {{modifier}} ViewArray<RedView> Ast{{field.Name}}
                {
                    get
                    {
                        if (_ast{{backingFieldName}} == null)
                        {
                            var _positionOfField = base.GetPositionFor({{field.ChildIndex}});
                            _ast{{backingFieldName}} = new ViewArray<RedView>({{greenField}}, _positionOfField, this);
                        }
                        return _ast{{backingFieldName}}.Value;
                    }
                }
                private global::System.Collections.Immutable.ImmutableArray<{{fieldTypeName}}>? {{backingFieldName}} = null;
                {{modifier}} global::System.Collections.Immutable.ImmutableArray<{{fieldTypeName}}> {{field.Name}}
                {
                    get
                    {
                        if ({{backingFieldName}} == null)
                        {
                            var _tmp = Ast{{field.Name}}.Where(static (_, i) => i % 2 == 0).Cast<{{fieldTypeName}}>();
                            {{backingFieldName}} = global::System.Collections.Immutable.ImmutableArray.ToImmutableArray(_tmp);
                        }
                        return {{backingFieldName}}.Value;
                    }
                }
                """);
            }
        }

        close();
    }

    internal void AddUnionType(TypeIr ir)
    {
        string modifierName = ir.AccessModifier.CodeRepresentation();
        string typeNodeName = nodeName(ir.Name, true);

        beginLine();
        add($"{modifierName} partial interface {typeNodeName} : IGreenNode");
        foreach (string union in ir.UnionMembership.Select(u => nodeName(u, true)))
        {
            add($", {union}");
        }
        add(";");
        endLine();

        string typeViewName = viewName(ir.Name, true);

        beginLine();
        add($"{modifierName} partial interface {typeViewName} : IRedView");
        foreach (string union in ir.UnionMembership.Select(u => viewName(u, true)))
        {
            add($", {union}");
        }
        add(";");
        endLine();
    }

    private static string nodeName(string original, bool union = false)
        => (union ? "I" : "") + original + "Node";

    private static string viewName(string original, bool isUnion = false)
        => (isUnion ? "I" : "") + original + "View";

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
