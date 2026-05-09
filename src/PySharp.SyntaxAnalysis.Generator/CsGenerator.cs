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

        // Generate auto-inferred types.
        foreach (var tp in grammar.Types)
            addType(tp);

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
            addAlternative(alternative, rule.ReturnName);
            close();
            addLine("Reset(__mark);");
        }
        addLine("return null;");
        close();
        addLine("#endregion");
    }

    #region Each alternative

    private void addAlternative(AlternativeData alternative, string returnType)
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
        // string ctorArgs = string.Join(", ", alternative.CtorVariables);
        // return new {{returnType}}({{ctorArgs}})
        if (!alternative.HasOptionals)
        {
            string children = string.Join(", ",
                alternative.Variables.Select(static v => v.NeedWrapper
                ? $"new NodeArrayWrapNode({v.Name})"
                : v.Name));
            addLines($$"""
            return {{alternative.ReturnExpression}}
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
                        return $"new NodeArrayWrapNode({v.Name})";
                    else
                        return v.Name;
                }));
            addLines($$"""
            List<GreenNode> __children = [{{children}}];
            __children.RemoveAll(static __node => __node is null);
            return {{alternative.ReturnExpression}}
            {
                Children = new NodeArray<GreenNode>(__children)
            };
            """);
        }
        close();
    }

    private void addCondition(ConditionData cond)
    {
        switch (cond.Kind)
        {
            case ConditionKind.Expect:
                if (cond.IsToken)
                    addLine(isNotNull($"{cond.AssignedVar} = Expect(TokenType.{cond.CallData})"));
                else
                    addLine(isNotNull($"{cond.AssignedVar} = Expect({cond.CallData})"));
                break;

            case ConditionKind.Rule:
                addLine(isNotNull($"{cond.AssignedVar} = rule_{cond.CallData}()"));
                break;

            case ConditionKind.Lookahead:
                string lookArg = cond.IsString ? cond.CallData :
                                cond.IsToken ? $"TokenType{cond.CallData}" :
                                $"rule_{cond.CallData}";
                string truthy = cond.Positive!.Value ? "true" : "false";
                addLine($"Lookahead({lookArg}, {truthy})");
                break;

            case ConditionKind.Repeat:
                string repArg = cond.IsString ? cond.CallData :
                                cond.IsToken ? $"TokenType{cond.CallData}" :
                                $"rule_{cond.CallData}";
                addLine(isNotNull($"{cond.AssignedVar} = Repeat({repArg}, {cond.MinCount})"));
                break;

            case ConditionKind.Optional:
                if (cond.IsString && cond.IsToken)
                    throw new ArgumentException("cond.IsString and cond.IsToken cannot be enabled both at the same time.", nameof(cond));

                if (cond.IsString)
                    addLine(wrapOpt($"{cond.AssignedVar} = Expect({cond.CallData})"));
                else if (cond.IsToken)
                    addLine(wrapOpt($"{cond.AssignedVar} = Expect(TokenType.{cond.CallData})"));
                else
                    addLine(wrapOpt($"{cond.AssignedVar} = rule_{cond.CallData}()"));

                break;
                static string wrapOpt(string orig) => $"(({orig}) is not null || true) // Optional";
        }
    }

    private static string isNotNull(ReadOnlySpan<char> value) => $"({value}) is not null";

    #endregion

    #region Type generation

    private void addType(TypeData tp)
    {
        addLine($"internal record __PegenGenerated_{tp.Name} : GreenNode");
        open();
        foreach (var field in tp.Fields)
            addLine($"internal {createTypeName(field)} {field.Name} {{ get; private init; }}");

        addLine($"internal {tp.Name}({tp.Fields.Select(static v => $"{createTypeName(v)} {v.Name}")})");
        open();
        foreach (var field in tp.Fields)
            addLine($"this.{field.Name} = {field.Name};");

        close();
        close();

        static string createTypeName(VariableData var) => $"{var.TypeName}{(var.IsOptional ? "?" : "")}";
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
