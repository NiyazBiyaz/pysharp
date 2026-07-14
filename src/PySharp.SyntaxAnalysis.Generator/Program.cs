using CommandLine;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;
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
            Environment.Exit(1);
        }

        var binder = new Binder();

        try
        {
            binder.ReadMetadata(grammarParsed.Metadata);
            binder.RegisterRules(grammarParsed.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
            binder.InspectRules();
        }
        catch (CompilationException e)
        {
            Console.Error.WriteLine($"Error at line {e.Line}: {e.Message}");
            Environment.Exit(1);
        }

        foreach (var warn in binder.Warnings)
        {
            Console.WriteLine($"Warning at line {warn.Line}: {warn.Message}");
        }

        var boundGrammar = binder.Grammar;

        var fileGenerator = new CsGenerator();

        fileGenerator.AddFileHeader(boundGrammar.UserHeader!, grammarPath);

        string generatedGrammar = boundGrammar.GenerateCode();

        fileGenerator.AddFileBody(generatedGrammar);

        File.WriteAllText(outputPath, fileGenerator.Dump());
    }
}
