using System.Text;
using PySharp.SyntaxAnalysis.Tokens;

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

            var sync = SynchronizationPoint.ClearPoint(new StringBuffer(source));

            var tokenizer = new Tokenizer(sync, true);
            List<Token> tokens = [];

            var startTime = DateTime.UtcNow;

            while (!tokenizer.ShouldStop)
                tokens.Add(tokenizer.ReadNext());

            var endTime = DateTime.UtcNow;

            var file = File.CreateText(outPath);
            file.NewLine = "\n";
            foreach (var token in tokens)
                file.WriteLine(token.MakeItNoice(120));

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
            Console.WriteLine("exit\nBau!");
        };

        string? input;
        while (true)
        {
            Console.Write(">>> ");
            input = Console.ReadLine();

            if (input is string source && !source.SequenceEqual("exit"))
            {
                var sync = SynchronizationPoint.ClearPoint(new StringBuffer(source));

                var tokenizer = new Tokenizer(sync, false);

                while (!tokenizer.ShouldStop)
                {
                    var tok = tokenizer.ReadNext();
                    if (tok.Type == TokenType.Error)
                        Console.WriteLine($"Bad token: {tokenizer.ErrorMessage}");
                    Console.WriteLine(tok.MakeItNoice(Console.WindowWidth));
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

public static class TokenExtensions
{
    extension(Token token)
    {
        public string MakeItNoice(int width)
        {
            StringBuilder builder = new(width);

            builder.Append(token.Type);

            while (builder.Length < 20)
            {
                builder.Append(' ');
            }

            if (token.Lexeme.Length > 0)
            {
                builder.Append($"'{token.Lexeme.ToString().Replace("\n", "\\n").Replace("\r", "\\r")}'");
            }

            builder.Append("        ");

            string start = $"Start: {token.Start.Line},{token.Start.Column}";
            string end = $"End: {token.End.Line},{token.End.Column}";
            int together = start.Length + end.Length + 1;

            while (together + builder.Length + 1 < width)
            {
                builder.Append(' ');
            }

            builder.Append(start);
            builder.Append(' ');
            builder.Append(end);

            return builder.ToString();
        }
    }
}
