using System.Diagnostics;
using System.Text;
using PySharp.SyntaxAnalysis.Common.Ast;

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
                foreach (var sourceLine in alt.SourceText.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    addLine($"// {sourceLine}");

                openBlock();

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
                            // IsArray means that we need repeat, virtual means we need lookahead.
                            Debug.Assert(q.IsArray != q.IsVirtual);

                            if (q.IsArray)
                            {
                                string innerInterpolation = q.Inner switch
                                {
                                    RuleSymbolIr r => $"rule_{r.Rule.Name}",
                                    TokenSymbolIr t => t.ExpectInterpolation,
                                    QuantifiedSymbolIr => throw new UnreachableException("Inner quantifiers is not allowed."),
                                    _ => throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type."),
                                };
                                addLine(checkNull($"{q.Name} = Repeat({innerInterpolation}, {q.RepeatCount})"));
                            }
                            else
                            {
                                string innerInterpolation = q.Inner switch
                                {
                                    RuleSymbolIr r => $"rule_{r.Name}",
                                    TokenSymbolIr t => $"Expect({t.ExpectInterpolation})",
                                    QuantifiedSymbolIr => throw new UnreachableException("Inner quantifiers is not allowed."),
                                    _ => throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type."),
                                };
                                string positiveness = q.Positiveness!.Value ? "true" : "false";
                                addLine($"Lookahead({innerInterpolation}, {positiveness})");
                            }
                            break;

                        default:
                            throw new UnreachableException($"Unexpected {nameof(ISymbolIr)} instance type.");
                    }
                }
                indentLevel -= 1;
                addLine(")");

                // Return on success.
                openBlock();
                addLine($"return {alt.SuccessExpression}"); // TODO: Allow not to write 'new' in the grammar.
                openBlock();

                var children = alt.Symbols.Select(sym =>
                {
                    if (sym.IsArray)
                        return $"new {nameof(NodeArrayWrapNode)}({sym.Name})";
                    else
                        return sym.Name;
                });
                addLine($"Children = new NodeArray<GreenNode>([{string.Join(", ", children)}])");

                // Manually decrease for add semicolon.
                indentLevel -= 1;
                addLine("};");
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

    private void add(string value) => builder.Append(value);
}
