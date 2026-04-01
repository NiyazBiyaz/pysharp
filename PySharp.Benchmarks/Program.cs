using BenchmarkDotNet.Running;

namespace PySharp.Benchmarks;

public static class Program
{
    public static void Main()
    {
        var summary = BenchmarkRunner.Run<BenchTokenizer>();
    }
}
