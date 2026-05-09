using CommandLine;

namespace PySharp.SyntaxAnalysis.Generator;

internal class CliOptions
{
    [Option('i', "input", Required = true, HelpText = "Grammar file path.")]
    public string GrammarPath { get; set; } = null!;

    [Option('o', "output", Required = true, HelpText = "Output C# file path.")]
    public string OutputPath { get; set; } = null!;

    [Option('f', "force", Required = false, Default = false, HelpText = "Overwrite existing output file, if exists.")]
    public bool ForceOverwrite { get; set; }
}
