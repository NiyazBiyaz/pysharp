namespace PySharp.SyntaxAnalysis.Generator;

internal class VariableNamingScope
{
    private int overallCount = 0;
    private readonly HashSet<string> existing = [];

    public string NextName(string original)
    {
        overallCount += 1;
        string name = original = original.ToLowerInvariant();
        int suffixNumber = 1;
        while (existing.Contains(name))
            name = original + suffixNumber++;

        existing.Add(name);
        return name;
    }

    public string NextNamePreserveCase(string original)
    {
        overallCount += 1;
        string name = original;
        int suffixNumber = 1;
        while (existing.Contains(name))
            name = original + suffixNumber++;

        existing.Add(name);
        return name;
    }

    public string NextString() => $"__token{overallCount++}";

    public string NextTypeName() => $"_PegenNetAnonymousType{overallCount++}";
}
