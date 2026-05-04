using System.Diagnostics;
using System.Text;
using PySharp.SyntaxAnalysis.Generator.Ast;

namespace PySharp.SyntaxAnalysis.Generator;

internal class CodeGenerator
{
    private readonly StringBuilder builder = new();
    private readonly Dictionary<string, RuleNode> rules = [];
    private int indentLevel = 0;

    // Metadata properties
    private readonly string userHeader;
    private readonly string classSignature;
    private readonly string parseCallReturn;
    private readonly string tokenTypePrefix = "TokenType.";

    private readonly Dictionary<string, string> strAliases = [];

    private const string parse_call_signature = "public override {0}? Parse() => rule_Start();";
    private const string indent_string = "    ";
    private const string generated_comment_header = """
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

    public CodeGenerator(GrammarNode grammar)
    {
        foreach (var meta in grammar.Metadata)
        {
            switch (meta.Name)
            {
                case "header":
                    userHeader = meta.StringValue;
                    break;

                case "class_signature":
                    classSignature = meta.StringValue;
                    break;

                case "token_type_prefix":
                    tokenTypePrefix = meta.StringValue;
                    break;

                case "parse_call_return":
                    parseCallReturn = meta.StringValue;
                    break;

                default:
                    throw new ArgumentException("Unknown metadata type.");
            }
        }
        if (userHeader is null)
            throw new IncompleteMetadataException("header");
        if (classSignature is null)
            throw new IncompleteMetadataException("class_signature");
        if (parseCallReturn is null)
            throw new IncompleteMetadataException("parse_call_return");

        foreach (var alias in grammar.Aliases)
        {
            strAliases[alias.OldValue] = alias.NewValue;
        }

        // Make searching by name easier.
        foreach (var rule in grammar.Rules)
        {
            if (rules.ContainsKey(rule.Name))
                throw new ArgumentException("Grammar cannot contain 2 rules with the same name.");
            rules[rule.Name] = rule;
        }

        if (!rules.ContainsKey("Start"))
        {
            throw new ArgumentException("Grammar should contain one 'Start' rule.");
        }
    }

    public string Generate(string grammarName)
    {
        // Generate headers part.
        addLine(string.Format(generated_comment_header, grammarName));
        addLine(userHeader);
        addLine(classSignature);
        addLine("{");
        indentLevel += 1;

        // Generate rules.
        foreach (var rule in rules.Values)
        {
            addLine($"{rule.TypeSpec.TypeName}? rule_{rule.Name}()");

            addLine("{"); // Open bracket
            indentLevel += 1;

            addLine("int mark = Mark();"); // Mark backtracking.

            foreach (var alter in rule.Alternatives)
            {
                // Show source text of the alternate.
                addLine($"{{ // {alter.RecoverText()[1..^1]}"); // Cut '|' at the start and new line at the end.
                indentLevel += 1;

                // Allocate/declare variables of the alternative.
                List<string> variables = [];
                List<bool> wraps = [];
                int lookaheadCount = 0;
                foreach ((int varNumber, var molecule) in alter.Molecules.Index())
                {
                    // Lookahead can't have node value, so skip it declaration.
                    string nameForUser = molecule switch  // TODO: Lookahead will explode
                    {
                        AtomMoleculeNode hydrogen => constructNameForAtom(hydrogen.Atom, varNumber),
                        RepeatOneMoreNode repeat1 => constructNameForAtom(repeat1.Atom, varNumber) + "Plus",
                        RepeatZeroMoreNode repeat0 => constructNameForAtom(repeat0.Atom, varNumber) + "Star",
                        _ => throw new UnreachableException("Ты наверное добавил новую молекулу, но забыл обновить здесь"),
                    };
                    // If some names are duplicates, we need to make inclusive by adding number (starting by 1).
                    // First duplicate wouldn't have number.
                    string nameForUserCopy = nameForUser;
                    int addNumber = 1;
                    while (variables.Contains(nameForUser))
                    {
                        nameForUser = nameForUserCopy + addNumber;
                        addNumber += 1;
                    }
                    variables.Add(nameForUser);

                    // Declare variables.
                    if (molecule is AtomMoleculeNode h && rules.TryGetValue(h.Atom.Value, out var ruleForType))
                    {
                        wraps.Add(isNodeArray(ruleForType.TypeSpec.TypeName));
                        addLine($"{ruleForType.TypeSpec.TypeName}? {variables[varNumber]};");
                    }
                    else if (molecule is RepeatMoleculeNode repeat)
                    {
                        wraps.Add(true);
                        if (rules.TryGetValue(repeat.Atom.Value, out var _ruleForType))
                            addLine($"NodeArray<{_ruleForType.TypeSpec.TypeName}>? {variables[varNumber]};");

                        else
                            addLine($"NodeArray<TokenNode>? {variables[varNumber]};");
                    }
                    else
                    {
                        wraps.Add(false);
                        addLine($"TokenNode? {variables[varNumber - lookaheadCount]};");
                    }
                }

                // Condition checking.
                // To make generating easier, 'if' contains 'true' at first line of condition.
                // Probably it would be optimized.
                addLine("if (true");
                indentLevel += 1;
                foreach ((var molecule, string name) in alter.Molecules.Zip(variables))
                {
                    addLine("&& " + callMolecule(molecule, name));
                }
                indentLevel -= 1;
                addLine(")"); // -Condition checking.

                actionReturn(rule, alter, variables, wraps);

                indentLevel -= 1; // -Alter.
                addLine("}");
                failAlternative();
            }

            addLine("return null;");

            indentLevel -= 1;
            addLine("}"); // Close bracket
            addLine(""); // Space to make it a little more readable
        }

        addLine(string.Format(parse_call_signature, parseCallReturn));
        addLine("protected override HashSet<string> Keywords => [];");

        // End generation.
        indentLevel -= 1;
        addLine("}");

        return builder.ToString();
    }

    private string constructNameForAtom(AtomNode atom, int varNumber) => atom switch
    {
        StringAtomNode str => aliasOrMangle(str, varNumber).ToLowerInvariant(),
        NameAtomNode name => name.Value.ToLowerInvariant(),
        _ => throw new UnreachableException("Ты наверное что-то добавил, но не обновил здеся"),
    };

    private void actionReturn(RuleNode rule, AlternativeNode alter, List<string> variables, List<bool> wraps)
    {
        addLine("{");
        indentLevel += 1;

        if (!isNodeArray(rule.TypeSpec.TypeName))
        {
            // TODO: Now i'm suppress it, but action may be null.
            // In that case we should expose parsed nodes to upper function.
            // For it we need synthetic rules generation, but it will be later.
            addLine($"return {alter.Action!.Expression}");

            addLine("{");
            indentLevel += 1;

            var wrappedVar = variables
                .Zip(wraps, (name, wrap) => wrap ? $"new NodeArrayWrapNode({name})" : name);

            addLine($"Children = new NodeArray<GreenNode>([{string.Join(", ", wrappedVar)}])");

            indentLevel -= 1;
            addLine("};");
        }
        else
            addLine($"return {alter.Action!.Expression};"); // TODO: see above.

        indentLevel -= 1;
        addLine("}");
    }

    private string aliasOrMangle(StringAtomNode str, int suffix)
    {
        if (strAliases.TryGetValue(str.Parsed, out string? rename))
            return rename;
        else
            return $"t_{suffix}";
    }

    private void failAlternative() => addLine("Reset(mark);");

    private string callMolecule(MoleculeNode molecule, string varName)
    {
        return molecule switch
        {
            AtomMoleculeNode hydrogen => callAtom(hydrogen.Atom).WrapNullCheck(varName), // Hydrogen because it has exactly one atom!
            RepeatOneMoreNode repeat1 => callRepeat(repeat1.Atom, 1).WrapNullCheck(varName),
            RepeatZeroMoreNode repeat0 => callRepeat(repeat0.Atom, 0).WrapNullCheck(varName),
            _ => throw new UnreachableException("расширь там короче, ты обосрался слегка"),
        };
    }

    private string callAtom(AtomNode atom)
    {
        if (atom is NameAtomNode nameAtom)
        {
            if (rules.ContainsKey(nameAtom.Value))
                return $"rule_{nameAtom.Value}()";
            else
                return $"Expect({tokenTypePrefix}{nameAtom.Value})";
        }
        else if (atom is StringAtomNode stringAtom)
        {
            if (strAliases.TryGetValue(stringAtom.Parsed, out string? tokName))
                return $"Expect({tokenTypePrefix}{tokName})";

            return $"Expect({stringAtom.Value})";
        }

        else
            throw new UnreachableException("Unexpected AtomNode subclass.");
    }

    private string callRepeat(AtomNode atom, int minimalCount)
    {
        if (atom is NameAtomNode name)
        {
            if (rules.TryGetValue(name.Value, out _))
                return $"Repeat(rule_{name.Value}, {minimalCount})";

            else
                return $"Repeat({tokenTypePrefix}{name.Value}, {minimalCount})";
        }
        else if (atom is StringAtomNode str)
            return $"Repeat({str.Value}, {minimalCount})";

        else
            throw new UnreachableException("Тут пока не доделано. Наверное должны быть другие атомы");
    }

    private void addLine(string value)
    {
        if (value.Length > 0)
            for (int i = 0; i < indentLevel; i++)
                add(indent_string);

        builder.AppendLine(value);
    }

    private void add(string value) => builder.Append(value);

    private static bool isNodeArray(string typeName) => typeName.StartsWith("NodeArray<");
}
