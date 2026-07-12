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

    internal void AddCreationAction(string returnTypeName, IEnumerable<VariableIr> variables)
    {
        AddLine($"_res = new {returnTypeName}()");
        open();
        AddLine("Children = new NodeArray<GreenNode>([");
        indentation++;
        foreach (var variable in variables)
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

    internal void AddPassAction(string name)
    {
        AddLine($"_res = {name};");
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
                throw new UnreachableException($"Unexpected ConditionKind: {conditionIr.Kind}");
        }

        static string wrapOpt(ReadOnlySpan<char> value) => $"(({value}) is not null || true) // Optional";
        static string wrapNull(ReadOnlySpan<char> value) => $"({value}) is not null";
    }

    internal void AddAlternative(string originalText, IEnumerable<VariableIr> variables, IEnumerable<string> conditionEmits, string? actionEmit)
    {
        foreach (var line in originalText.Trim().EnumerateLines())
        {
            AddLine($"// {line}");
        }

        foreach (var varEmit in variables)
        {
            if (varEmit.IsArray)
                AddLine($"INodeArray<GreenNode>? {varEmit.Name};");
            else
                AddLine($"GreenNode? {varEmit.Name};");
        }

        if (conditionEmits.Count() == 1)
        {
            addLines($"""
            if ({conditionEmits.First()})
            """);
        }
        else
        {
            AddLine($"if ({conditionEmits.First()}");
            indentation++;
            foreach (var condEmit in conditionEmits.Skip(1))
            {
                AddLine("&&");
                AddLine(condEmit);
            }
            indentation--;
            AddLine(")");
        }

        open();
        addLines(actionEmit);
        close();
    }

    internal void AddRuleHeader(RuleIr ruleIr)
    {
        AddLine($"#region {ruleIr.Name}");

        if (ruleIr.IsMemoEnabled)
        {
            AddLine($"private readonly IMemoContainer<{ruleIr.ReturnTypeName}> _memo_{ruleIr.Name} = CreateContainer<{ruleIr.ReturnTypeName}>();");
        }

        foreach (var line in ruleIr.OriginalText.Trim('\n', '\r', ' ', '\t').EnumerateLines())
            AddLine($"// {line}");

        AddLine($"{ruleIr.ReturnTypeName}? rule_{ruleIr.Name}()");
    }

    internal void AddRuleBody(RuleIr ir, IEnumerable<(string alternativeText, bool hasCut)> alternativeEmits)
    {
        open();

        AddLine("int _mark = base.Mark();");

        if (ir.IsMemoEnabled)
        {
            addLines($$"""
            if (_memo_{{ir.Name}}.TryGetCache(_mark, out {{ir.ReturnTypeName}}? _memoized))
            {
                return _memoized;
            }
            """);
        }

        AddLine($"{ir.ReturnTypeName}? _res = null;");

        if (alternativeEmits.Any(ae => ae.hasCut))
            AddLine("bool _cut = false;");

        foreach (var (altText, hasCut) in alternativeEmits)
        {
            open();
            addLines(altText);
            close();

            AddLine("base.Reset(_mark);");
            if (hasCut)
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
        if (ir.IsMemoEnabled)
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

        AddLine($"{modifierName} class {parserName}(ITokenNodeStream _tokenStream) : BaseParser<{topLevelNodeName}>(_tokenStream)");
    }

    internal void AddParserBody(string mainName, string mainTypeName, IEnumerable<string> ruleEmits, IEnumerable<string> keywords)
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

        foreach (var emitRule in ruleEmits)
        {
            addBlankLine();
            addLines(emitRule);
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

    internal void AddTypes(IEnumerable<string> typeEmits)
    {
        if (!typeEmits.Any())
            return;

        bool addBlank = false;

        foreach (var typeEmit in typeEmits)
        {
            if (addBlank)
                addBlankLine();
            addBlank = true;
            addLines(typeEmit);
        }
    }

    internal void AddTypeBody(IEnumerable<FieldIr> fields)
    {
        open();

        foreach (var field in fields)
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

    internal void AddTypeSignature(AccessModifier accessModifier, string typeName, string? baseName, bool isAbstract, IEnumerable<string> unionMembership)
    {
        string modifierName = accessModifier.CodeRepresentation();

        baseName ??= nameof(GreenNode);

        beginLine();
        add($"{modifierName} {(isAbstract ? "abstract" : "sealed")} partial record {typeName} : {baseName}");
        foreach (var union in unionMembership)
            add($", {union}");
        endLine();
    }

    internal void AddUnion(AccessModifier accessModifier, string typeName, IEnumerable<string> unionMembership)
    {
        string modifierName = accessModifier.CodeRepresentation();

        beginLine();
        add($"{modifierName} partial interface {typeName} : IGreenNode");
        foreach (var union in unionMembership)
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
