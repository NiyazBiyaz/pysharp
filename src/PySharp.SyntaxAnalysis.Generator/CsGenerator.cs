using System.Diagnostics;
using System.Text;

namespace PySharp.SyntaxAnalysis.Generator;

internal class CsGenerator(GrammarData grammar)
{
    private readonly GrammarData grammar = grammar;
    private readonly StringBuilder builder = new();

    private int indent
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            field = value;
        }
    } = 0;

    private const string parse_method_template = "public override {0}? Parse() => rule_{1}();";

    private string getMetaOrThrow(string key)
    {
        try
        {
            return grammar.MetadataFields[key];
        }
        catch (KeyNotFoundException)
        {
            throw new IncompleteMetadataException(key);
        }
    }

    public string Generate(string grammarPath)
    {
        // Generate header.
        addLines($"""
        // This file was automatically generated from {grammarPath}
        // Bau bau!
        #nullable enable
        """);
        addLine(getMetaOrThrow("header"));

        // Setup parser class.
        addLine(getMetaOrThrow("class_signature"));

        open();

        addLine(string.Format(parse_method_template,
            getMetaOrThrow("parse_call_return"),
            getMetaOrThrow("main_rule_name")));
        string keywords = string.Join(", ", grammar.Keywords.Select(k => $"\"{k}\""));
        addLine($"protected override HashSet<string> Keywords {{ get; }} = [{keywords}];");

        // Generate all rules.
        foreach (var rule in grammar.Rules)
            addRule(rule);

        close();

        // Generate all types.
        foreach (var type in grammar.Types)
            addType(type);

        Debug.Assert(indent == 0);
        return builder.ToString();
    }

    private void addRule(RuleData rule)
    {
        addLine($"#region {rule.Name}");
        addLine($"{rule.ReturnName}? rule_{rule.Name}()");
        open();
        addLine("int __mark = Mark();");
        foreach (var alternative in rule.Alternatives)
        {
            open();
            addAlternative(alternative, rule.IsUnion, rule.IsAnonymous);
            close();
            addLine("Reset(__mark);");
        }
        addLine("return null;");
        close();
        addLine("#endregion");
    }

    #region Each alternative

    private void addAlternative(AlternativeData alternative, bool union, bool anonymous)
    {
        addLine($"// {alternative.OriginalText.ReplaceLineEndings("\\n")}");

        foreach (var varDecl in alternative.Variables)
        {
            if (!varDecl.NeedWrapper)
                addLine($"{varDecl.TypeName}? {varDecl.Name};");
            else
                addLine($"NodeArray<{varDecl.TypeName}>? {varDecl.Name};");
        }

        addLine("if (");
        indent++;
        bool needAndOperator = false;
        foreach (var cond in alternative.Conditions)
        {
            if (needAndOperator)
                addLine("&&");

            needAndOperator = true;
            addCondition(cond);
        }
        indent--;
        addLine(")");

        open();
        if (union)
        {
            // Since every union rule should have exactly one physical variable, we can take it to return.
            var capturedVariable = alternative.Variables.First();
            addLine($"return {capturedVariable.Name};");
        }
        else if (!alternative.HasOptionals)
        {
            string children = string.Join(", ",
                alternative.Variables.Select(static v => v.NeedWrapper
                ? $"new NodeList({v.Name})"
                : v.Name));

            addReturnExpression();

            addLines($$"""
            {
                Children = new NodeArray<GreenNode>([{{children}}])
            };
            """);
        }
        else
        {
            string children = string.Join(", ", alternative.Variables
                .Select(static v =>
                {
                    if (v.IsOptional)
                        return v.Name + "!";
                    else if (v.NeedWrapper)
                        return $"new NodeList({v.Name})";
                    else
                        return v.Name;
                }));

            addLines($"""
            List<GreenNode> __children = [{children}];
            __children.RemoveAll(static __node => __node is null);
            """);

            addReturnExpression();

            addLines($$"""
            {
                Children = new NodeArray<GreenNode>(__children)
            };
            """);
        }
        close();

        void addReturnExpression()
        {
            beginLine();
            add($"return new {alternative.ReturnTypeName}(");
            bool comma = false;
            foreach (var arg in alternative.CtorArguments)
            {
                if (comma)
                    add(", ");
                comma = true;
                add(arg.CtorArgumentType switch
                {
                    CtorArgumentType.Raw => arg.VariableName,
                    CtorArgumentType.String => $"{arg.VariableName}.RawString",
                    CtorArgumentType.ParseString => $"StringParser.ParseQuotedString({arg.VariableName}.RawString)",
                    CtorArgumentType.WrapArray => $"[{arg.VariableName}]",
                    CtorArgumentType.GroupAxis => $"[.. {arg.VariableName}.Select(static ___ => ___.{arg.AxisName})]",
                    CtorArgumentType.GroupAxisString =>
                        $"[.. {arg.VariableName}.Select(static ___ => ___.{arg.AxisName}.RawString)]",
                    CtorArgumentType.GroupAxisParseString =>
                        $"[.. {arg.VariableName}.Select(static ___ => StringParser.ParseQuotedString(___.{arg.AxisName ?? throw new Exception("Ты почему такой нулевый")}.RawString))]",
                    CtorArgumentType.BoolConstant => arg.BoolConstant!.Value ? "true" : "false",
                    _ => throw new UnreachableException($"Unexpected value of CtorArgumentType: {arg.CtorArgumentType}")
                });
            }
            add(")");
            endLine();
        }
    }

    private void addCondition(ConditionData cond)
    {
        switch (cond.Kind)
        {
            case ConditionKind.Expect:
                if (cond.Atom.IsToken)
                    addLine(isNotNull($"{cond.AssignedVar} = Expect(TokenType.{cond.Atom.CallData})"));
                else
                    addLine(isNotNull($"{cond.AssignedVar} = Expect({cond.Atom.CallData})"));
                break;

            case ConditionKind.Rule:
                addLine(isNotNull($"{cond.AssignedVar} = rule_{cond.Atom.CallData}()"));
                break;

            case ConditionKind.Lookahead:
                string lookArg = constructArg(cond.Atom);
                string truthy = cond.Positive!.Value ? "true" : "false";
                addLine($"Lookahead({lookArg}, {truthy})");
                break;

            case ConditionKind.Repeat:
                string repArg = constructArg(cond.Atom);
                addLine(isNotNull($"{cond.AssignedVar} = Repeat({repArg}, {cond.MinCount})"));
                break;

            case ConditionKind.Optional:
                if (cond.Atom.IsString && cond.Atom.IsToken)
                    throw new ArgumentException("cond.IsString and cond.IsToken cannot be enabled both at the same time.", nameof(cond));

                if (cond.Atom.IsString)
                    addLine(wrapOpt($"{cond.AssignedVar} = Expect({cond.Atom.CallData})"));
                else if (cond.Atom.IsToken)
                    addLine(wrapOpt($"{cond.AssignedVar} = Expect(TokenType.{cond.Atom.CallData})"));
                else
                    addLine(wrapOpt($"{cond.AssignedVar} = rule_{cond.Atom.CallData}()"));

                break;
                static string wrapOpt(string orig) => $"(({orig}) is not null || true) // Optional";
            case ConditionKind.Gather:
                string valuedArg = constructArg(cond.Atom);
                string separator = constructArg(cond.Separator!);

                addLine(isNotNull($"{cond.AssignedVar} = Gather({valuedArg}, {separator})"));

                break;
        }

        static string constructArg(AtomData atom)
        {
            return atom.IsString ? atom.CallData :
                   atom.IsToken ? $"TokenType.{atom.CallData}" :
                   $"rule_{atom.CallData}";
        }
    }

    private static string isNotNull(ReadOnlySpan<char> value) => $"({value}) is not null";

    #endregion

    #region Types generation

    private void addType(TypeData type)
    {
        string modifier = type.AccessModifier switch
        {
            TypeAccessModifier.Anonymous => "internal",
            TypeAccessModifier.Public => "public",
            _ => throw new UnreachableException("Unexpected TypeAccessModifier value."),
        };
        var fieldsWithType = type.Fields.Select(f =>
        {
            var fieldType = f.TypeName;
            if (f.NeedWrapper)
                fieldType = $"NodeArray<{fieldType}>";
            if (f.IsOptional)
                fieldType = $"{fieldType}?";

            return $"{fieldType} {f.Name}";
        });

        addLine($"#region type {type.Name}");
        addLine($"{modifier} record {type.Name} : GreenNode");
        open();

        foreach (var fwt in fieldsWithType)
        {
            beginLine();
            add($"{modifier} ");
            add($"{fwt} {{ get; private init; }}");
            endLine();
        }

        addLine($"{modifier} {type.Name}({string.Join(", ", fieldsWithType)})");
        open();
        foreach (var field in type.Fields)
            addLine($"this.{field.Name} = {field.Name};");
        close();

        close();
        addLine("#endregion");
    }

    #endregion

    private const string indent_string = "    ";
    private const string line_feed = "\n";

    private void open()
    {
        addLine("{");
        indent += 1;
    }

    private void close()
    {
        indent -= 1;
        addLine("}");
    }

    private void beginLine()
    {
        for (int i = 0; i < indent; i++)
            add(indent_string);
    }

    private void endLine() => add(line_feed);

    private void addLines(ReadOnlySpan<char> value)
    {
        foreach (var line in value.EnumerateLines())
        {
            if (line.Length > 0)
                addLine(line);
        }
    }

    private void addLine(ReadOnlySpan<char> value)
    {
        beginLine();
        add(value);
        endLine();
    }

    private void add(ReadOnlySpan<char> value) => builder.Append(value);
}
