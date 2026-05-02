using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Generator;
using PySharp.SyntaxAnalysis.Tokens;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Specify grammar file path.");
            Environment.Exit(1);
        }

        string grammarPath = args.First();

        // string grammarPath = "/home/biyaz/Projects/PySharp/meta.gram";

        if (!File.Exists(grammarPath))
        {
            Console.Error.WriteLine($"File '{grammarPath}' doesn't exists.");
            Environment.Exit(2);
        }

        string grammar = File.ReadAllText(grammarPath);

        var gramBuffer = new StringBuffer(grammar);
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(gramBuffer), false);
        var tokenStream = new TokenNodeStream(tokenizer);
        var parser = new GrammarParser(tokenStream);

        var grammarParsed = parser.Start();

        Console.WriteLine(grammarParsed);
    }
}
