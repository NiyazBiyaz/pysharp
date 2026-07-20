using System.CommandLine;
using System.Runtime.CompilerServices;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;

[assembly: InternalsVisibleTo("PySharp.SyntaxAnalysis.Generator.Tests")]

namespace PySharp.SyntaxAnalysis.Generator;

internal class Program
{
    private static void Main(string[] args)
    {
        RootCommand root = new("PEG parser compiler-like generator PegenNet inspired by CPython's pegen.");

        Argument<FileInfo> grammarInput = new("Grammar file")
        {
            Description = "Path to the grammar file generate parser to.",
        };

        root.Add(grammarInput);

        Option<string> parserOutput = new("--output", "-o")
        {
            Description = "Path to file of generated parser."
        };

        root.Add(parserOutput);

        Option<bool> forceOption = new("--force", "-f")
        {
            Description = "Force to overwrite parser file if it exists."
        };

        root.Add(forceOption);

        root.SetAction(parseResult =>
        {
            string? output = parseResult.GetValue(parserOutput);
            var grammarFile = parseResult.GetValue(grammarInput) ?? throw new NullReferenceException("Given argument is null.");
            bool forced = parseResult.GetValue(forceOption);

            if (!grammarFile.Exists)
            {
                Console.Error.WriteLine($"File '{grammarFile.FullName}' does not exists.");
                Environment.Exit(1);
            }

            string grammar = grammarFile.OpenText().ReadToEnd();

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

            var grammarView = grammarParsed.GetView(TokenPosition.StartOfFile, null);

            try
            {
                binder.ReadMetadata(grammarView.Metadata);
                binder.RegisterRules(grammarView.Rules);
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

            fileGenerator.AddFileHeader(boundGrammar.UserHeader!, grammarFile.Name);

            string generatedGrammar = boundGrammar.GenerateCode();

            fileGenerator.AddFileBody(generatedGrammar);

            string outputPath = output ?? boundGrammar.ParserName + ".g.cs";

            if (File.Exists(outputPath) && !forced)
            {
                Console.Error.WriteLine($"File '{outputPath}' already exists. Use --force flag to overwrite it.");
                Environment.Exit(1);
            }

            File.WriteAllText(outputPath, fileGenerator.Dump());
        });

        root.Parse(args).Invoke();
    }
}
