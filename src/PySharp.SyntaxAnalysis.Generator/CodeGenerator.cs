using System.Diagnostics;
using System.Text;
using PySharp.SyntaxAnalysis.Common.Ast;
using PySharp.SyntaxAnalysis.Generator.Intermediate;

namespace PySharp.SyntaxAnalysis.Generator;

internal class CodeGenerator
{
    private readonly StringBuilder builder = new();
    private int indentLevel = 0;

    private readonly IEnumerable<IRuleIr> rules;

    // Metadata properties
    private readonly string userHeader;
    private readonly string classSignature;
    private readonly string parseCallReturn;

    private const string parse_call_signature = "public override {0}? Parse() => rule_Start();";
    private const string indent_string = "    ";
    private const string comment_easter_egg = """
    // This file was generated from '{0}'.
    // СВИНОЙ ШАР
    // ⠀⠀⠀⠀⠀⠀⠀⠀⣠⣤⣴⣾⣿⡿⣟⣯⢿⠾⣙⠒⠢⢄⡀⠀⠀⠀⠀⠀⠀⠀
    // ⠀⠀⠀⠀⢀⣠⣶⣿⣿⣿⣿⡿⣷⣟⡿⣞⣯⣿⣭⠷⣆⡄⡈⠑⠦⡀⠀⠀⠀⠀
    // ⠀⠀⠀⣴⣿⣿⣿⣿⣿⡿⣿⣽⡿⣾⣿⢿⣻⡾⣽⡻⣭⢷⡑⢦⡀⠉⢦⡀⠀⠀
    // ⠀⢀⣾⣿⣿⣿⣿⣿⡏⠉⠁⠉⠉⠛⣿⣿⠟⠉⠉⠁⠀⠉⢻⡆⡕⢣⡀⢳⡀⠀
    // ⠀⣾⣿⣿⣿⣿⣿⣿⣿⣿⣿⣶⣶⣶⣿⣷⣶⣤⣦⣶⣦⣤⣌⡳⡘⢧⡘⡄⢳⠀
    // ⢸⣿⣿⣿⣿⣿⣿⣿⣿⣏⠉⠉⣿⡯⢽⣻⣿⣿⣍⠀⠉⣹⠏⠙⢿⣢⢝⡰⢈⣇
    // ⣿⣿⣿⣿⣿⣿⣿⣿⣿⣿⣟⣫⣵⠶⠶⠿⠶⣽⣿⣿⣿⣿⣀⣤⡾⣓⢮⡔⠈⠀
    // ⣿⣿⣿⣿⣿⢻⢿⡻⣟⣿⢫⣷⣿⣿⣿⣿⣿⣶⣽⣿⣿⣿⣿⠆⢻⡹⡖⣍⠂⠀
    // ⣿⣿⣿⣿⣿⢯⣾⣿⣿⡇⣿⣇⣀⣿⣿⣦⣤⣿⡇⣿⡿⠛⣿⠠⢡⢳⡙⠦⠁⠀
    // ⠸⣿⣿⣿⣯⢿⣳⣿⣿⣿⢙⠿⠿⠿⠛⠻⠟⠋⠁⢁⢈⣾⢮⠑⣎⡙⢂⠁⠀
    // ⠀⢿⣿⣳⢯⣟⣯⡷⢯⣿⣯⡴⣤⣤⣤⣤⣴⣶⡿⣡⣾⢌⠢⡙⠤⠑⠀⠀⠀
    // ⠀⠈⢿⣟⡿⣞⣷⣻⣽⢯⣿⣾⣭⣭⣭⣭⣷⣾⣿⢮⢋⢆⠓⡨⠐⠁⡠⠀⠀
    // ⠀⠀⠀⠻⣿⡽⢯⣟⡾⢯⢞⡯⣝⢣⠏⢮⠑⠣⠍⢎⠳⠌⠢⠁⢐⡤⠛⠀⠀⠀
    // ⠀⠀⠀⠀⠀⠛⢿⣘⠹⢎⠳⡜⢤⢃⠎⠤⢉⠐⠠⠀⠀⠀⣠⠖⠋⠀⠀⠀⠀⠀
    // ⠀⠀⠀⠀⠀⠀⠀⠈⠉⠓⠒⠀⠂⠈⠈⠀⠀⠀⠀⠀⠀⠉⠀⠀⠀
    """;

    public CodeGenerator(GrammarIr grammar)
    {
        userHeader = grammar.Header;
        classSignature = grammar.ClassSignature;
        parseCallReturn = grammar.ParseCallReturnType;
        rules = grammar.Rules;
    }

    public string Generate(string grammarName)
    {
        // Generate headers part.
        addLine(string.Format(comment_easter_egg, grammarName));
        addLine("#nullable enable");
        addLine(userHeader);
        addLine(classSignature);
        openBlock();

        foreach (var rule in rules)
        {
            addLine($"{rule.ReturnType}? rule_{rule.Name}()");
            openBlock();
            addLine("int __mark = Mark();");

            foreach (var alt in rule.Alternatives)
            {
                openBlock();
                foreach (var sourceLine in alt.SourceText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    addLine($"// {sourceLine}");

                // Allocate variables;
                foreach (var sym in alt.Symbols.Where(s => !s.IsVirtual))
                    addLine($"{sym.TypeName}? {sym.Name};");

                // Check condition;
                addLine("if (true");
                indentLevel += 1;
                foreach (var sym in alt.Symbols)
                {
                    switch (sym)
                    {
                        case TokenSymbolIr t:
                            addLine(checkNull($"{t.Name} = Expect({t.ExpectInterpolation})"));
                            break;
                        case RuleSymbolIr r:
                            addLine(checkNull($"{r.Name} = rule_{r.Rule.Name}()"));
                            break;
                        case QuantifiedSymbolIr q:
                            Debug.Assert(q.IsVirtual || q.Kind != QuantifierKind.Lookahead);

                            string innerInterpolation = q.Inner switch
                            {
                                RuleSymbolIr r => $"rule_{r.Rule.Name}",
                                TokenSymbolIr t => t.ExpectInterpolation,
                                QuantifiedSymbolIr => throw new UnreachableException("Inner quantifiers is not allowed."),
                                _ => throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type."),
                            };
                            switch (q.Kind)
                            {
                                case QuantifierKind.Repeat:
                                    Debug.Assert(q.RepeatCount is not null);
                                    addLine(checkNull($"{q.Name} = Repeat({innerInterpolation}, {q.RepeatCount})"));
                                    break;
                                case QuantifierKind.Lookahead:
                                    Debug.Assert(q.Positiveness is not null);
                                    addLine($"&& Lookahead({innerInterpolation}, {(q.Positiveness.Value ? "true" : "false")})");
                                    break;
                                case QuantifierKind.Optional:
                                    innerInterpolation = q.Inner switch
                                    {
                                        RuleSymbolIr => innerInterpolation + "()",
                                        TokenSymbolIr t => $"Expect({innerInterpolation})",
                                        _ => throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type."),
                                    };
                                    addLine($"&& (({q.Name} = {innerInterpolation}) is not null || true) // Optional");
                                    break;
                            }
                            break;

                        default:
                            throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type.");
                    }
                }
                indentLevel -= 1;
                addLine(")");

                openBlock(); // Success action.
                var children = alt.Symbols
                    .Where(sym => !sym.IsVirtual)
                    .Select(sym =>
                    {
                        if (sym is QuantifiedSymbolIr q && q.Kind == QuantifierKind.Repeat)
                            return $"new {nameof(NodeArrayWrapNode)}({sym.Name})";
                        else
                            return sym.Name;
                    });
                if (alt.Symbols.All(s => s is not QuantifiedSymbolIr q || q.Kind != QuantifierKind.Optional))
                {
                    // TODO: Allow not to write 'new' in the grammar.
                    addLines($$"""
                    return {{alt.SuccessExpression}}
                    {
                        Children = new NodeArray<GreenNode>([{{string.Join(", ", children)}}])
                    };
                    """);
                }
                else
                {
                    addLines($$"""
                    List<GreenNode> children = [{{string.Join("!, ", children)}}];
                    children.RemoveAll(static child => child is null);
                    return {{alt.SuccessExpression}} { Children = new NodeArray<GreenNode>(children) };
                    """);
                }
                closeBlock();

                closeBlock();
                addLine("Reset(__mark);");
            }

            addLine("return null;");
            closeBlock();
            addLine("");
        }

        addLine(string.Format(parse_call_signature, parseCallReturn));
        addLine("protected override HashSet<string> Keywords => [];");
        closeBlock();

        Debug.Assert(indentLevel == 0);

        return builder.ToString();
    }

    private static string checkNull(string expr) => $"&& ({expr}) is not null";

    private void openBlock()
    {
        addLine("{");
        indentLevel += 1;
    }

    private void closeBlock()
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(indentLevel, nameof(indentLevel));
        indentLevel -= 1;
        addLine("}");
    }

    private void addLine(string value)
    {
        if (value.Length > 0)
            for (int i = 0; i < indentLevel; i++)
                add(indent_string);

        builder.AppendLine(value);
    }

    private void addLines(string value)
    {
        foreach (var line in value.EnumerateLines())
        {
            beginLine();
            builder.Append(line);
            endLine();
        }
    }

    private void beginLine()
    {
        for (int i = 0; i < indentLevel; i++)
            add(indent_string);
    }

    private void endLine() => add("\n");

    private void add(string value) => builder.Append(value);
}
