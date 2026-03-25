using PySharp.Tokens;

namespace PySharp;

public static class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
            runPrompt();
        else
            runFile(args);
    }

    private static void runFile(string[] args)
    {
        foreach (string arg in args)
        {
            if (!File.Exists(arg))
            {
                Console.WriteLine($"File {arg} does not exists.");
                Environment.Exit(2);
            }

            string source = File.ReadAllText(arg);

            string outPath = arg + ".tokens";

            var tokenizer = new Tokenizer(source, true);
            List<Token> tokens = [];

            var startTime = DateTime.UtcNow;

            while (!tokenizer.ShouldStop)
                tokens.Add(tokenizer.ReadNext());

            var endTime = DateTime.UtcNow;

            var file = File.CreateText(outPath);
            file.NewLine = "\n";
            foreach (var token in tokens)
                file.WriteLine(token);

            file.Flush();
            file.Close();

            Console.WriteLine($"File {arg} was parsed to tokens.");
            Console.WriteLine($"Time elapsed: {(endTime - startTime).TotalMicroseconds}μs.");
            Console.WriteLine($"Result was saved to {outPath}.");
        }
    }

    private static void runPrompt()
    {
        Console.CancelKeyPress += (_, _) =>
        {
            Console.WriteLine("Bau!");
        };

        string? input;
        while (true)
        {
            Console.Write(">>> ");
            input = Console.ReadLine();

            if (input is string source && !source.SequenceEqual("exit"))
            {
                var tokenizer = new Tokenizer(source, false);

                while (!tokenizer.ShouldStop)
                {
                    var tok = tokenizer.ReadNext();
                    Console.WriteLine(tok);
                }
            }
            else
            {
                Console.Write("exit");
                break;
            }
        }

        Console.WriteLine("\nBau!");
    }
}
