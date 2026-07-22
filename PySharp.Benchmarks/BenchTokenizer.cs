using BenchmarkDotNet.Attributes;
using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.Benchmarks;

[MemoryDiagnoser]
public class BenchTokenizer
{
    private static readonly string source_with_f_strings;
    private static readonly string source_without_f_strings;

    static BenchTokenizer()
    {
        source_with_f_strings = File.ReadAllText("/your/personal/path/to/file/with/fstrings");
        source_without_f_strings = File.ReadAllText("/your/personal/path/to/file/without/fstrings");
    }

    [Benchmark]
    public void TestTokenizeFStrings()
    {
        var buffer = new StringBuffer(source_with_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, true);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext(out _);
    }

    [Benchmark]
    public void TestTokenizeRegular()
    {
        var buffer = new StringBuffer(source_without_f_strings);
        var sync = SynchronizationPoint.ClearPoint(buffer);

        var tokenizer = new Tokenizer(sync, true);

        while (!tokenizer.ShouldStop)
            tokenizer.ReadNext(out _);
    }
}
