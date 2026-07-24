using PySharp.SyntaxAnalysis;
using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp;

public static class Program
{
    public static void Main(string[] args) => runFile(args);

    private static void runFile(string[] args)
    {
        foreach (string arg in args)
        {
            if (!File.Exists(arg))
            {
                Console.WriteLine($"File {arg} does not exists.");
                Environment.Exit(1);
            }

            string source = File.ReadAllText(arg);

            string outPath = arg + ".ptree";

            var sync = SynchronizationPoint.ClearPoint(new StringBuffer(source));

            var tokenizer = new Tokenizer(sync);
            var parser = new PythonParser(new TokenNodeStream(tokenizer));

            var startTime = DateTime.UtcNow;

            var tree = parser.Parse();

            var endTime = DateTime.UtcNow;

            if (tree != null)
            {
                var file = File.CreateText(outPath);
                file.NewLine = "\n";
                file.WriteLine(tree.PrettyPrint());

                file.Flush();
                file.Close();

                Console.WriteLine($"File {arg} was parsed.");
                Console.WriteLine($"Time elapsed: {(endTime - startTime).TotalMilliseconds}ms.");
                Console.WriteLine($"Result was saved to {outPath}.");
            }
            else
            {
                Console.WriteLine($"Parsing error in file {arg}");
            }
        }
    }
}
