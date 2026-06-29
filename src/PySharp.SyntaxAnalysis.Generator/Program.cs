using CommandLine;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PySharp.SyntaxAnalysis.Generator.Tests")]

namespace PySharp.SyntaxAnalysis.Generator;

internal class Program
{
    private static void Main(string[] args) =>
    Parser.Default.ParseArguments<CliOptions>(args).WithParsed(opt =>
    {
        if (!File.Exists(opt.GrammarPath))
        {
            Console.Error.WriteLine($"Error: File '{opt.GrammarPath}' does not exists.");
            return;
        }
        if (File.Exists(opt.OutputPath) && !opt.ForceOverwrite)
        {
            Console.Error.WriteLine($"Error: File '{opt.OutputPath}' already exists. Use '--force' to force overwriting file.");
            return;
        }

        run(opt.GrammarPath, opt.OutputPath);
    });

    private static void run(string grammarPath, string outputPath)
    {
        string grammar = File.ReadAllText(grammarPath);

        var gramBuffer = new StringBuffer(grammar);
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(gramBuffer), true);
        var tokenStream = new TokenNodeStream(tokenizer);
        var parser = new GrammarParser(tokenStream);

        var grammarParsed = parser.Parse();

        if (grammarParsed is null)
        {
            Console.Error.WriteLine($"Parsing error. Line: {tokenizer.Synchronize().StartLine + 1}");
            Environment.Exit(3);
        }

        var binder = new Binder();
        binder.ReadMetadata(grammarParsed.Metadata);
        binder.RegisterRules(grammarParsed.Rules);
        binder.PopulateRules();
        binder.CreateCaptures();

        var boundGrammar = binder.Grammar;

        var fileGenerator = new CsGenerator();

        fileGenerator.AddFileHeader(boundGrammar.UserHeader!, grammarPath);

        string parserFile = createParser(fileGenerator, boundGrammar);

        File.WriteAllText(outputPath, parserFile);
    }

    private static string createParser(CsGenerator fileGenerator, BoundGrammar boundGrammar)
    {
        var parserGenerator = new CsGenerator();
        parserGenerator.AddParserSignature(AccessModifier.Internal, boundGrammar.ParserName!, boundGrammar.TopLevelNodeName!);

        List<string> ruleEmits = [];
        foreach (var rule in boundGrammar.Rules)
        {
            ruleEmits.Add(createRule(rule));
        }

        parserGenerator.AddParserBody(boundGrammar.MainRule?.Name ?? throw new NullReferenceException(), boundGrammar.MainRule.TypeName,
                                      ruleEmits, []);

        fileGenerator.AddParser(parserGenerator.Dump());

        return fileGenerator.Dump();
    }

    private static string createRule(BoundRule rule)
    {
        var ruleGenerator = new CsGenerator();
        var ir = new RuleIr(rule.SourceText, rule.Name, rule.TypeName);
        ruleGenerator.AddRuleHeader(ir);

        var altEmits = rule.Alternatives.Select(alt =>
        {
            List<VariableIr> variables = alt.Entries
                .Select(v => new VariableIr(v.Name, v.Quantifier.IsArray, v.Quantifier is QuantifierKind.Optional))
                .ToList();

            var actionGenerator = new CsGenerator();

            actionGenerator.AddAction(alt.Action!.TypeHint, variables);

            List<string> conditions = alt.Entries.Select(createCondition).ToList();

            var altGenerator = new CsGenerator();

            altGenerator.AddAlternative(alt.SourceText, variables, conditions, actionGenerator.Dump());

            return (altGenerator.Dump(), alt.Entries.Any(e => e.Quantifier == QuantifierKind.Cut));
        });
        ruleGenerator.AddRuleBody(altEmits);
        ruleGenerator.AddRuleEnd(ir);

        return ruleGenerator.Dump();
    }

    private static string createCondition(BoundAlternativeEntry alternativeEntry)
    {
        static AtomIr getAtom(BoundAlternativeEntry alternativeEntry) => alternativeEntry switch
        {
            BoundTokenAlternativeEntry token => new AtomIr(token.Value.ToString(), false, true),
            BoundStringAlternativeEntry str => new AtomIr(str.Value, true, false),
            BoundRuleAlternativeEntry rule => new AtomIr(rule.Value.Name, false, false),
            BoundGatherAlternativeEntry gath => getAtom(gath.Value),
            _ => throw new UnreachableException("Unexpected bound alternative entry class."),
        };

        var conditionIr = new ConditionIr
        {
            Kind = alternativeEntry.Quantifier,
            MinCount = alternativeEntry.MinRepeatCount,
            Positive = alternativeEntry.Positiveness,
            AssignedVar = alternativeEntry.Name,
            Atom = getAtom(alternativeEntry),
            Separator = alternativeEntry is BoundGatherAlternativeEntry gath ? getAtom(gath.Separator) : null,
        };

        var condGenerator = new CsGenerator();

        condGenerator.AddCondition(conditionIr);

        return condGenerator.Dump();
    }
}
