using System.Diagnostics;
using System.Text;

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
            if (variable.IsArray)
                varName = $"new NodeList({varName})";
            else if (variable.IsOptional)
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
        AddLine($"_res = {action.Variables.First()};");
        AddLine("goto _Return;");
    }

    internal void AddCondition(ConditionIr conditionIr)
    {
        switch (conditionIr.Kind)
        {
            case QuantifierKind.Expect:
                if (conditionIr.Atom.IsString)
                    add(wrapNull($@"{conditionIr.AssignedVar} = Expect(""{conditionIr.Atom.CallData}"")"));
                else if (conditionIr.Atom.IsToken)
                    add(wrapNull($"{conditionIr.AssignedVar} = Expect(TokenType.{conditionIr.Atom.CallData})"));
                else
                    add(wrapNull($"{conditionIr.AssignedVar} = rule_{conditionIr.Atom.CallData}()"));
                break;

            case QuantifierKind.Lookahead:
                string positive = conditionIr.Positive!.Value ? "true" : "false";
                if (conditionIr.Atom.IsString)
                    add($@"Lookahead(""{conditionIr.Atom.CallData}"", {positive})");
                else if (conditionIr.Atom.IsToken)
                    add($"Lookahead({conditionIr.Atom.CallData}, {positive})");
                else
                    add($"Lookahead(rule_{conditionIr.Atom.CallData}, {positive})");
                break;

            case QuantifierKind.Repeat:
                if (conditionIr.Atom.IsString)
                    add(wrapNull($@"{conditionIr.AssignedVar} = base.Repeat(""{conditionIr.Atom.CallData}"", {conditionIr.MinCount})"));
                else if (conditionIr.Atom.IsToken)
                    add(wrapNull($"{conditionIr.AssignedVar} = base.Repeat(TokenType.{conditionIr.Atom.CallData}, {conditionIr.MinCount})"));
                else
                    add(wrapNull($"{conditionIr.AssignedVar} = base.Repeat(rule_{conditionIr.Atom.CallData}, {conditionIr.MinCount})"));
                break;

            case QuantifierKind.Optional:
                if (conditionIr.Atom.IsString)
                    add(wrapOpt($@"{conditionIr.AssignedVar} = Expect(""{conditionIr.Atom.CallData}"")"));
                else if (conditionIr.Atom.IsToken)
                    add(wrapOpt($"{conditionIr.AssignedVar} = Expect(TokenType.{conditionIr.Atom.CallData})"));
                else
                    add(wrapOpt($"{conditionIr.AssignedVar} = rule_{conditionIr.Atom.CallData}()"));
                break;

            case QuantifierKind.Gather:
                string atom = (conditionIr.Atom.IsString, conditionIr.Atom.IsToken) switch
                {
                    (false, false) => $"rule_{conditionIr.Atom.CallData}",
                    (true, false) => $@"""{conditionIr.Atom.CallData}""",
                    (false, true) => $"TokenType.{conditionIr.Atom.CallData}",
                    (true, true) => throw new UnreachableException("Invalid atom flags state: Token can't be both string and TokenType"),
                };

                if (conditionIr.Separator is null)
                    throw new UnreachableException("condition.Separator should not be null here.");

                string separator = (conditionIr.Separator.IsString, conditionIr.Separator.IsToken) switch
                {
                    (false, false) => $"rule_{conditionIr.Separator.CallData}",
                    (true, false) => $@"""{conditionIr.Separator.CallData}""",
                    (false, true) => $"TokenType.{conditionIr.Separator.CallData}",
                    (true, true) => throw new UnreachableException("Invalid atom flags state: Token can't be both string and TokenType"),
                };

                add(wrapNull($"{conditionIr.AssignedVar} = base.Gather({atom}, {separator})"));
                break;

            case QuantifierKind.Cut:
                add("(_cut = true)");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(conditionIr), $"Unexpected ConditionKind: {conditionIr.Kind}");
        }

        static string wrapOpt(ReadOnlySpan<char> value) => $"(({value}) is not null || true) // Optional";
        static string wrapNull(ReadOnlySpan<char> value) => $"({value}) is not null";
    }

    internal void AddAlternative(AlternativeIr ir)
    {
        foreach (var line in ir.SourceText.Trim().EnumerateLines())
        {
            AddLine($"// {line}");
        }

        foreach (var varEmit in ir.Variables)
        {
            if (varEmit.IsArray)
                AddLine($"INodeArray<IGreenNode>? {varEmit.Name};");
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

        if (ir.Action.Kind == ActionKind.Generative)
            AddGenerativeAction(ir.Action);

        else
            AddPassAction(ir.Action);

        close();
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

        foreach (var line in ir.OriginalText.Trim('\n', '\r', ' ', '\t').EnumerateLines())
            AddLine($"// {line}");

        string rawPrefix = ir.IsLeftRecursive ? "raw_" : "";

        AddLine($"{ir.ReturnTypeName}? {rawPrefix}rule_{ir.Name}()");
    }

    private void addLeftRecursionWrapper(RuleIr ir)
    {
        AddLine($"{ir.ReturnTypeName}? rule_{ir.Name}()");

        open();

        addLines($$"""
        {{ir.ReturnTypeName}}? _res = null;
        int _mark = base.Mark();
        int _lastMark = base.Mark();
        if (_memo_{{ir.Name}}.TryGetCache(_mark, out var _memoized))
        {
            return _memoized;
        }
        while (true)
        {
            _memo_{{ir.Name}}.UpdateCache(_mark, _res);
            base.Reset(_mark);
            var _rawResult = raw_rule_{{ir.Name}}();
            if (_rawResult == null || base.Mark() <= _lastMark)
            {
                break;
            }
            _lastMark = base.Mark();
            _res = _rawResult;
        }
        base.Reset(_lastMark);
        return _res;
        """);

        close();
    }

    internal void AddRuleBody(RuleIr ir)
    {
        open();

        AddLine("int _mark = base.Mark();");

        if (ir.IsMemoEnabled && !ir.IsLeftRecursive)
        {
            addLines($$"""
            if (_memo_{{ir.Name}}.TryGetCache(_mark, out {{ir.ReturnTypeName}}? _memoized))
            {
                return _memoized;
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

        indentation--;
        if (ir.IsMemoEnabled && !ir.IsLeftRecursive)
        {
            addLines($"""
            _Return:
                _memo_{ir.Name}.AddCache(_mark, _res);
                return _res;
            """);
        }
        else
        {
            AddLine("""
            _Return:
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
                    {modifier} NodeArray<{field.TypeName}> {field.Name} => ((NodeList)Children![{field.ChildIndex}]).GetArray<{field.TypeName}>();
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
                    {{modifier}} NodeArray<GreenNode> Ast{{field.Name}} => (NodeArray<GreenNode>)((NodeList)Children![{{field.ChildIndex}}]).Children!;
                    """);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        close();
    }

    // internal void AddTypeSignature(, , , , )
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
}
