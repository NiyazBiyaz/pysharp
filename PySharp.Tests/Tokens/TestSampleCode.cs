using System.Text.Json;
using System.Text.Json.Serialization;
using PySharp.Tokens;

namespace PySharp.Tests.Tokens;

public class TestSampleCode
{
    private static readonly JsonSerializerOptions options;

    static TestSampleCode()
    {
        options = new();
        options.Converters.Add(new ReadOnlyMemoryConverter());
        options.Converters.Add(new JsonStringEnumConverter());
    }

    [Theory]
    [InlineData("TokensSample1")]
    [InlineData("pkgutil")]
    public void Test(string name)
    {
        string codePath = Path.Combine(AppContext.BaseDirectory, "Data", name);
        string tokensPath = Path.Combine(AppContext.BaseDirectory, "Data", name + ".json");

        string code = File.ReadAllText(codePath);

        var tokensFile = File.ReadAllText(tokensPath);
        var tokensData = JsonSerializer.Deserialize<SampleTokensData>(tokensFile, options);

        ArgumentNullException.ThrowIfNull(tokensData);

        var expected = tokensData.Tokens;

        var sync = SynchronizationPoint.ClearPoint(new StringBuffer(code));

        var tokenizer = new Tokenizer(sync, true);
        List<Token> tokens = new(expected.Count);

        while (!tokenizer.ShouldStop)
        {
            var tok = tokenizer.ReadNext();
            // CPython does not generate whitespace tokens.
            if (tok.Type != TokenType.WhiteSpace && tok.Type != TokenType.DebugSpecifierString)
                tokens.Add(tok);
        }

        Assert.Equal(TokenizerError.NoError, tokenizer.Error);
        Assert.All(tokens, (t) => Assert.NotEqual(TokenType.Error, t.Type));

        Assert.Equal(expected.Count, tokens.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            Token exp = expected[i], tok = tokens[i];

            tok = tok with
            {
                Start = tok.Start with { Line = tok.Start.Line + 1 },
                End = tok.End with { Line = tok.End.Line + 1 }
            };

            Assert.Equal(exp.Type, tok.Type);
            Assert.Equal(exp.Lexeme, tok.Lexeme);
            Assert.Equal(exp.Start, tok.Start);
            Assert.Equal(exp.End, tok.End);
        }
    }
}

public record SampleTokensData(string Name, List<Token> Tokens);

public class ReadOnlyMemoryConverter : JsonConverter<ReadOnlyMemory<char>>
{
    public override ReadOnlyMemory<char> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string str = reader.GetString() ?? "";
        return str.AsMemory();
    }

    public override void Write(Utf8JsonWriter writer, ReadOnlyMemory<char> value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Span);
}
