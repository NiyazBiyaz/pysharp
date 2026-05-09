using CommandLine;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Generator;
using PySharp.SyntaxAnalysis.Tokens;

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
            Console.Error.WriteLine("Parsing error.");
            Environment.Exit(3);
        }

        var grammarCompiler = new GrammarCompiler(grammarParsed);
        var grammarCompiled = grammarCompiler.Compile();

        var csGenerator = new CsGenerator(grammarCompiled);
        File.WriteAllText(outputPath, csGenerator.Generate(grammarPath));
    }
}
