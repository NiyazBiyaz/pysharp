using PySharp.SyntaxAnalysis.Common;
using PySharp.SyntaxAnalysis.Tokens;
using static PySharp.SyntaxAnalysis.Tokens.TokenType;

namespace PySharp.SyntaxAnalysis.Generator.Tests;

public class TestBinder
{
    [Fact]
    public void TestReadMetadata_HappyPath()
    {
        const string src = """
        @header "Bau-bauder"
        @parser_name "BauParser"
        """;
        var gram = getNode(src);
        var binder = new Binder();
        binder.ReadMetadata(gram.Metadata);
        Assert.Equal("Bau-bauder", binder.Grammar!.UserHeader);
        Assert.Equal("BauParser", binder.Grammar!.ParserName);
    }

    [Fact]
    public void TestReadMetadata_BadNames()
    {
        string src = """
        @bauder "Bau-bauder"
        @parser_name "BauParser"
        @top_level_node "BauNode"
        """;
        var gram = getNode(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });

        src = """
        @header "Bau-bauder"
        @bauser_name "BauParser"
        @top_level_node "BauNode"
        """;
        gram = getNode(src);
        binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });

        src = """
        @header "Bau-bauder"
        @parser_name "BauParser"
        @bau_level_node "BauNode"
        """;
        gram = getNode(src);
        binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });
    }

    [Fact]
    public void TestReadMetadata_Missing()
    {
        string src = """
        @parser_name "BauParser"
        """;
        var gram = getNode(src);
        var binder = new Binder();
        Assert.Throws<IncompleteMetadataException>(() => binder.ReadMetadata(gram.Metadata));

        src = """
        @header "Bau-bauder"
        """;
        gram = getNode(src);
        binder = new Binder();
        Assert.Throws<IncompleteMetadataException>(() => { binder.ReadMetadata(gram.Metadata); });
    }

    [Fact]
    public void TestRegisterRules_SimpleRules()
    {
        const string src = """
        BauBau: Bau Bau
        PonDeRing: Pon De Ring
        FuwaMoco: Fuwa Moco
        """;
        const int rules_count = 3;
        var gram = getNode(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(rules_count, binder.Rules.Count);
    }

    [Fact]
    public void TestRegisterRules_Groups()
    {
        const string src = """
        BauBau: Bau Bau
        PonDeRing: Pon (De Ring)+
        FuwaMoco_ch: Fuwa (Moco Chan)*
        """;
        const int rules_count = 5;
        var gram = getNode(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(rules_count, binder.Rules.Count);
    }

    [Fact]
    public void TestRegisterRules_NestedGroups()
    {
        const string src = """
        BauBauBauBauBau:
            | Bau (!(Bau Bau) Bau)+ Bau
        Bau: BauBau
        """;
        const int rules_count = 4;
        var gram = getNode(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(rules_count, binder.Rules.Count);
    }

    [Fact]
    public void TestRegisterRules_Reserved()
    {
        const string src = """
        Dot: Comma
        Name: "name"
        """;
        var gram = getNode(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() => binder.RegisterRules(gram.Rules));
    }

    [Fact]
    public void TestPopulateRules_TwoRule_OneLink()
    {
        const string src = """
        Bau1: "." NewLine
        Bau2: Bau1
        """;
        var gram = getNode(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        foreach (var rule in binder.Grammar.Rules)
        {
            switch (rule.Name)
            {
                case "Bau1":
                {
                    Assert.Equal("dot", rule.Alternatives.First().Entries[0].Name);
                    Assert.True(
                        rule.Alternatives.First().Entries[0] is BoundTokenAlternativeEntry t0 && t0.Value == TokenType.Dot,
                        "Binder should replace string that can be replaced with TokenType.");
                    Assert.Equal("newline", rule.Alternatives.First().Entries[1].Name);
                    Assert.True(rule.Alternatives.First().Entries[1] is BoundTokenAlternativeEntry t1 && t1.Value == TokenType.NewLine);
                    break;
                }
                case "Bau2":
                {
                    Assert.Equal("bau1", rule.Alternatives.First().Entries[0].Name);
                    Assert.True(
                        rule.Alternatives.First().Entries[0] is BoundRuleAlternativeEntry r0 && r0.Value == binder.Grammar.Rules[0],
                        "Entry Bau1 should reference to the rule Bau1 defined in the grammar."
                    );
                    break;
                }
            }
        }
    }

    [Theory]
    [InlineData(">", Greater)]
    [InlineData(".", Dot)]
    [InlineData(":", Colon)]
    [InlineData(":=", ColonEqual)]
    [InlineData("->", RightArrow)]
    public void TestPopulateRules_AliasesForDelimiters(string delim, TokenType tokenType)
    {
        string src = $"""
        Bau: "{delim}"
        """;
        var gram = getNode(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        var alternative = binder.Grammar.Rules.First().Alternatives.First();
        Assert.True(alternative.Entries.First() is BoundTokenAlternativeEntry token && token.Value == tokenType,
                    "Binder should replace string representation with the TokenType analogue.");
    }

    private static GrammarNode getNode(string src)
    {
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(new StringBuffer(src + '\n')), false);
        var parser = new GrammarParser(new TokenNodeStream(tokenizer));
        return parser.Parse() ?? throw new Exception("Given syntax is not valid.");
    }
}
