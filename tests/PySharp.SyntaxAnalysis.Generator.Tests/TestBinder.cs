using System.Diagnostics;
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
        var gram = getView(src);
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
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });

        src = """
        @header "Bau-bauder"
        @bauser_name "BauParser"
        @top_level_node "BauNode"
        """;
        gram = getView(src);
        binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });

        src = """
        @header "Bau-bauder"
        @parser_name "BauParser"
        @bau_level_node "BauNode"
        """;
        gram = getView(src);
        binder = new Binder();
        Assert.Throws<InvalidNameException>(() => { binder.ReadMetadata(gram.Metadata); });
    }

    [Fact]
    public void TestReadMetadata_Missing()
    {
        string src = """
        @parser_name "BauParser"
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<IncompleteMetadataException>(() => binder.ReadMetadata(gram.Metadata));

        src = """
        @header "Bau-bauder"
        """;
        gram = getView(src);
        binder = new Binder();
        Assert.Throws<IncompleteMetadataException>(() => { binder.ReadMetadata(gram.Metadata); });
    }

    [Fact]
    public void TestRegisterRules_SimpleRules()
    {
        const string src = """
        @main
        BauBau: Bau Bau
        PonDeRing: Pon De Ring
        FuwaMoco: Fuwa Moco
        """;
        const int rules_count = 3;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(rules_count, binder.Rules.Count);
        Assert.All(binder.Grammar.Rules, r => Assert.Equal(RuleKind.Type, r.Kind));
    }

    [Fact]
    public void TestRegisterRules_MissingMain()
    {
        const string src = """
        BauBau: Bau Bau
        Bau: "bau"
        """;
        var gram = getView(src);
        var binder = new Binder();

        Assert.Throws<CompilationException>(() => binder.RegisterRules(gram.Rules));
    }

    [Fact]
    public void TestRegisterRules_TwoMains()
    {
        const string src = """
        @main
        BauBau: Bau Bau
        @main
        Bau: "bau"
        """;
        var gram = getView(src);
        var binder = new Binder();

        Assert.Throws<CompilationException>(() => binder.RegisterRules(gram.Rules));
    }

    [Fact]
    public void TestRegisterRules_Groups()
    {
        const string src = """
        @main
        BauBau: Bau Bau
        PonDeRing: Pon (De Ring)+
        FuwaMoco_ch: Fuwa (Moco Chan)*
        """;
        const int rules_count = 5;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(rules_count, binder.Rules.Count);
    }

    [Fact]
    public void TestRegisterRules_NestedGroups()
    {
        const string src = """
        @main
        BauBauBauBauBau:
            | Bau (!(Bau Bau) Bau)+ Bau
        Bau: BauBau
        """;
        const int rules_count = 4;
        var gram = getView(src);
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
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() => binder.RegisterRules(gram.Rules));
    }

    [Fact]
    public void TestRegisterRules_InlineGroups()
    {
        const string src = """
        @main
        Bau: &(@inline "fluff" | "fuzz" | "mogojyan") Bau
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Equal(RuleKind.TokenUnion, binder.Grammar.Rules.First(r => r.IsGroup).Kind);
    }

    [Fact]
    public void TestRegisterRules_MemoFlag()
    {
        const string src = """
        @main
        @memo
        Bau: BauBau
        BauBau: "bau"
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.True(binder.Grammar.MainRule.EnableMemoization);
        Assert.False(binder.Grammar.Rules[1].EnableMemoization);
    }

    [Fact]
    public void TestPopulateRules_ReferenceToRule()
    {
        const string src = """
        @main
        Bau1: NewLine
        Bau2: Bau1
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        var rules = binder.Grammar.Rules;

        Assert.Equal(rules[0], ((BoundRuleAlternativeEntry)rules[1].Alternatives[0].Entries[0]).Value);
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
        @main
        Bau: "{delim}"
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        var alternative = binder.Grammar.Rules.First().Alternatives.First();
        Assert.True(alternative.Entries.First() is BoundTokenAlternativeEntry token && token.Value == tokenType,
                    "Binder should replace string representation with the TokenType analogue.");
    }

    [Fact]
    public void TestPopulateRules_Quantifiers()
    {
        const string src = """
        @main
        Bau: BauBau+
        BauBau: -"bau" baubau*
        baubau: PonDeRing+."Whaet"
        PonDeRing: &"Pon" !"De" "Ring"
        """;
        const int zero = 0, one = 1;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        var rules = binder.Grammar.Rules;

        Assert.Equal(QuantifierKind.Repeat, rules[0].Alternatives[0].Entries[0].Quantifier); // BauBau+
        Assert.Equal(one, rules[0].Alternatives[0].Entries[0].MinRepeatCount);

        Assert.Equal(QuantifierKind.Optional, rules[1].Alternatives[0].Entries[0].Quantifier); // -"bau"
        Assert.Equal(zero, rules[1].Alternatives[0].Entries[0].Index);

        Assert.Equal(QuantifierKind.Repeat, rules[1].Alternatives[0].Entries[1].Quantifier); // baubau*
        Assert.Equal(zero, rules[1].Alternatives[0].Entries[1].MinRepeatCount);
        Assert.Equal(one, rules[1].Alternatives[0].Entries[1].Index);

        Assert.Equal(QuantifierKind.Gather, rules[2].Alternatives[0].Entries[0].Quantifier); // PonGeRing*."Whaet"
        if (rules[2].Alternatives[0].Entries[0] is BoundGatherAlternativeEntry g)
        {
            Assert.True(g.Value is BoundRuleAlternativeEntry r && r.Value == rules[3],
                        "Should reference to the 'PonDeRing' rule defined below.");
            Assert.Equal(QuantifierKind.Expect, g.Separator.Quantifier);
        }
        else
            Assert.Fail("Should be gather.");

        Assert.Equal(QuantifierKind.Lookahead, rules[3].Alternatives[0].Entries[0].Quantifier); // &"Pon"
        Assert.Equal(true, rules[3].Alternatives[0].Entries[0].Positiveness);

        Assert.Equal(QuantifierKind.Lookahead, rules[3].Alternatives[0].Entries[1].Quantifier); // !"De"
        Assert.Equal(false, rules[3].Alternatives[0].Entries[1].Positiveness);

        Assert.Equal(QuantifierKind.Expect, rules[3].Alternatives[0].Entries[2].Quantifier); // "Ring"
        Assert.Equal(zero, rules[3].Alternatives[0].Entries[2].Index); // Lookaheads shouldn't affect on index counter.
    }

    [Fact]
    public void TestPopulateRules_NamesOfTheEntries()
    {
        const string src = """
        @main
        Bau: BauBau+
        BauBau: -Bau baubau*
        baubau: PonDeRing+."Whaet"
        PonDeRing: &"Pon" !"De" Ring
        Ring: "ring"
        """;
        string[][] names = [
            ["bau_bau_Plus"],
            ["bau", "baubau_Star"],
            ["pon_de_ring_Gather"],
            [null!, null!, "ring"], // For lookahead names are undefined.
            // For strings names are not guaranteed to be stable.
        ];
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        var rules = binder.Grammar.Rules;

        for (int r = 0; r < names.Length; r++)
        {
            var alt = rules[r].Alternatives[0];
            for (int e = 0; e < alt.Entries.Count; e++)
            {
                while (names[r][e] == null)
                    e++;
                Assert.Equal(names[r][e], alt.Entries[e].Name);
            }
        }
    }

    [Fact]
    public void TestPopulateRules_NameDoesNotExist()
    {
        const string src = """
        @main
        BauBau: BauBauBau Bau
        BauBauBau: "bau" "bau" "bau"
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);

        Assert.Throws<InvalidNameException>(binder.PopulateRules);
    }

    [Fact]
    public void TestCreateTypes_Simple()
    {
        const string src = """
        @main
        BauBau: "bau" Bau -> new(Bau=bau)
        Bau: "BAU" "(" Name "," Number ")" -> new(Tail=name, Ears=number)
        """,
        first_type_name = "BauBau",
        second_type_name = "Bau",
        first_type_field_name = "Bau",
        second_type_field_name1 = "Tail",
        second_type_field_name2 = "Ears";
        const int first_type_field_count = 1, second_type_field_count = 2;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        var types = binder.Grammar.Types.Cast<BoundRuleType>().ToList(); // There's no decorated rules.

        Assert.Equal(first_type_name, types[0].Name);
        Assert.Equal(second_type_name, types[1].Name);

        // If you force to use .Single() where is .Double() or .Triple()?
#pragma warning disable xUnit2013 // Do not use equality check to check for collection size.
        Assert.Equal(first_type_field_count, types[0].Fields.Count);
        Assert.Equal(second_type_field_count, types[1].Fields.Count);

        Assert.Equal(first_type_field_name, types[0].Fields[0].Name);
        Assert.Equal(second_type_field_name1, types[1].Fields[0].Name);
        Assert.Equal(second_type_field_name2, types[1].Fields[1].Name);
#pragma warning restore xUnit2013 // Do not use equality check to check for collection size.
    }

    [Fact]
    public void TestCreateTypes_WithBaseType()
    {
        const string src = """
        @main
        BauBau:
            | Number -> BauBauBau(Bau=number)
            | Name -> Third(BauBau=name)
            | Comma -> Fourth(Another=comma)
        """,
        base_type_name = "BauBau";
        const int types_count = 4;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        var types = binder.Grammar.Types.Cast<BoundRuleType>().ToList();

        Assert.Equal(types_count, types.Count);

        var baseType = types.First(t => t.Name == base_type_name);
        var derivedTypes = types.Where(t => t.Name != base_type_name);

        foreach (var dt in derivedTypes)
            Assert.Equal(baseType, dt.Base);
    }

    [Fact]
    public void TestCreateTypes_InferredWhenMultiple()
    {
        const string src = """
        @main
        BauBau:
            | Number -> BauBauBau(Bau=number)
            | Name -> new(BauBau=name)
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        Assert.Throws<CompilationException>(binder.CreateTypes);
    }

    [Fact]
    public void TestCreateTypes_NotSpecified()
    {
        const string src = """
        @main
        BauBau: Number Bau Name
        Bau: "Bau" "bau" "_bau"
        """,
        first_type_second_field_name = "Bau",
        token_node_type_base_name = "Token";
        const int both_type_fields_count = 3;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        var types = binder.Grammar.Types.Cast<BoundRuleType>().ToList();

        Assert.Equal(both_type_fields_count, types[0].Fields.Count);
        Assert.Equal(both_type_fields_count, types[1].Fields.Count);

        Assert.Equal(token_node_type_base_name, types[0].Fields[0].Name);
        Assert.Equal(first_type_second_field_name, types[0].Fields[1].Name);
        Assert.Equal(token_node_type_base_name + 1, types[0].Fields[2].Name);

        Assert.Equal(token_node_type_base_name, types[1].Fields[0].Name);
        Assert.Equal(token_node_type_base_name + 1, types[1].Fields[1].Name);
        Assert.Equal(token_node_type_base_name + 2, types[1].Fields[2].Name);
    }

    [Fact]
    public void TestCreateTypes_FieldNameIsDuplicated()
    {
        const string src = """
        @main
        BauBau: "bau" Number Name -> new(Bau=number, Bau=name)
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        Assert.Throws<InvalidNameException>(binder.CreateTypes);
    }

    [Fact]
    public void TestCreateTypes_UndefinedVariableUsage()
    {
        const string src = """
        @main
        BauBau: "bau" Number Name -> new(Bau=pondering, Another=name)
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();

        Assert.Throws<InvalidNameException>(binder.CreateTypes);
    }

    [Fact]
    public void TestCreateTypes_SingleArm_CanUseInferred()
    {
        const string src = """
        @main
        Bau:
            | "bau" Name -> new(Fluff=name)
        """,
            type_name = "Bau";
        const int type_count = 1;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

#pragma warning disable xUnit2013 // Do not use equality check to check for collection size.
        Assert.Equal(type_count, binder.Grammar.Types.Count);
        Assert.Equal(type_name, binder.Grammar.Types[0].Name);
#pragma warning restore xUnit2013 // Do not use equality check to check for collection size.
    }

    [Fact]
    public void TestCreateTypes_ForceToUseInferred()
    {
        const string src = """
        @main
        Bau: "bau" Name -> BauBau(Fluff=name)
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        Assert.Throws<CompilationException>(binder.CreateTypes);
    }

    [Fact]
    public void TestCreateTypes_GroupTypes()
    {
        const string src = """
        @main
        Bau: 'bau' ('fluff' Name -> Fuzz(Bau=name))
        """,
        group_type_name = "Fuzz";
        const int types_count = 2;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        Assert.Equal(types_count, binder.Grammar.Types.Count);
        Assert.Equal(group_type_name, binder.Grammar.Types[1].Name);
    }

    [Fact]
    public void TestCreateTypes_UndeclaredAction()
    {
        const string src = """
        @main
        BauBau:
            | 'bau' 'bau'
            | 'fluffy' 'fuzzy'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        // Created 3 types
        Assert.Equal(3, binder.Grammar.Types.Count);
        // Names of all types is unique.
        Assert.True(binder.Grammar.Types.TrueForAll(t => binder.Grammar.Types.Count(ot => ot.Name == t.Name) == 1));
    }

    [Fact]
    public void TestBinder_PreventUsingSameTypeNames_Rules()
    {
        const string src = """
        @main
        Bau: 'bau'
        Bau: 'Bau'
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_PreventUsingSameTypeNames_ActionsBetweenRules()
    {
        const string src = """
        @main
        Bau:
            | 'bau' -> Fluffy()
            | "hoeh" -> Fuzzy()
        BauBau:
            | 'whaet' -> Fluffy()
            | "iyargh" -> Hoeh()
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_PreventUsingSameTypeNames_ActionsInOneRule()
    {
        const string src = """
        @main
        Bau:
            | 'bau' -> BauBau()
            | "hoeh" -> BauBau()
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_PreventUsingSameTypeNames_RuleAndInAction()
    {
        const string src = """
        @main
        Bau:
            | 'bau' -> Fluffy()
            | "hoeh" -> Fuzzy()
        BauBau:
            | 'whaet' -> Bau()
            | "iyargh" -> Hoeh()
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidNameException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_UnionRule_OnlyOneVariableAllowed()
    {
        const string src = """
        @main
        @union
        BauBau:
            | Bau
            | PonDeRing
            | FluffyOne FuzzyOne

        Bau: 'bau'
        PonDeRing: 'pon' 'de' 'ring'
        FluffyOne: "fuwawae"
        FuzzyOne: 'mococoe'
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidUnionException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_InlineGroups_OnlyOneVariableAllowed()
    {
        const string src = """
        @main
        Bau: [@inline 'fuzz' 'fluff' | "baubau"]
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidUnionException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_UnionRule_ActionsIsNotAllowed()
    {
        const string src = """
        @main
        @union
        Bau:
            | BauBau -> BauBauBau(Value=bau_bau)
            | Whaet -> Hoeh(Fuzz=whaet)
        BauBau: 'bau' -> new()
        Whaet: "iyargh" -> new()
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidUnionException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_TokenUnionRule_ActionsIsNotAllowed()
    {
        const string src = """
        @main
        @inline
        Bau:
            | "BauBau" -> BauBauBau()
            | "Whaet" -> Hoeh()
        """;
        var gram = getView(src);
        var binder = new Binder();
        Assert.Throws<InvalidUnionException>(() =>
        {
            binder.RegisterRules(gram.Rules);
            binder.PopulateRules();
            binder.CreateTypes();
        });
    }

    [Fact]
    public void TestBinder_GroupRuleCreatedOnce_IfSameContent()
    {
        const string src = """
        @main
        Bau: [BauBau    'fluff'] # add spaces
        BauBau: 'fuzz' !(BauBau 'fluff')
        """;
        const int rules_count = 3;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        Assert.Equal(rules_count, binder.Grammar.Rules.Count);
        Assert.Equal(rules_count, binder.Grammar.Types.Count);
    }

    [Fact]
    public void TestBinder_GroupRuleCreatedTwice_IfHaveDecorator()
    {
        const string src = """
        @main
        Bau: [@inline "BauBau" | 'fluff']
        BauBau: 'fuzz' !("BauBau" | 'fluff')
        """;
        const int rules_count = 4;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        Assert.Equal(rules_count, binder.Grammar.Rules.Count);
    }

    [Fact]
    public void TestCreateTypes_UnionType_Membership()
    {
        const string src = """
        @main
        @union
        Bau:
            | BauBau
            | Fluff
            | Fuzz
        BauBau: "pondering"
        Fluff: "fuwawae"
        Fuzz: 'mogojyan'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        var union = binder.Grammar.Types.First(t => t.Name == "Bau");
        var rest = binder.Grammar.Types.Where(t => t.Name != "Bau");

        foreach (var member in rest)
        {
            Assert.Contains(union, member.UnionMembership);
            Assert.Contains(member, ((BoundUnionType)union).Members);
        }
    }

    [Fact]
    public void TestInspectRules_MarkAsLeftRecursive()
    {
        const string src = """
        @main
        Bau: 'bau' BauBau

        @memo
        BauBau:
            | BauBau 'fluff' 'fuzz'
            | 'fuzz'

        @memo
        BauBauBau:
            | 'fuzz'
            | BauBau 'fuzz'
            | 'fluff'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();
        binder.InspectRules();

        var normal = binder.Rules.Values.First(r => r.Name == "Bau");
        var leftRecursive1 = binder.Rules.Values.First(r => r.Name == "BauBau");
        var leftRecursive2 = binder.Rules.Values.First(r => r.Name == "BauBauBau");

        Assert.False(normal.IsLeftRecursive);
        Assert.True(leftRecursive1.IsLeftRecursive);
        Assert.False(leftRecursive2.IsLeftRecursive);
    }

    [Fact]
    public void TestInspectRules_RequireMemoOnLeftRecursive()
    {
        const string src = """
        @main
        Bau: 'bau' BauBau

        BauBau:
            | BauBau 'fluff' 'fuzz'
            | 'fuzz'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        Assert.Throws<CompilationException>(binder.InspectRules);
    }

    [Fact]
    public void TestInspectRules_ArmNeverReached()
    {
        const string src = """
        @main
        BauBau:
            | 'bau'
            | 'bau' 'halo'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();
        binder.InspectRules();

        Assert.Single(binder.Warnings);
    }

    [Fact]
    public void TestInspectRules_RuleNeverUsed()
    {
        const string src = """
        @main
        BauBau:
            | 'bau'
            | 'halo'

        Bau: 'fuwamocoe'
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();
        binder.InspectRules();

        Assert.Single(binder.Warnings);
        Assert.Contains("Bau", binder.Warnings[0].Message);
    }

    [Fact]
    public void TestInspectRules_DoNotUseMainInOtherRules()
    {
        const string src = """
        @main
        BauBau: Fluffy
        Fluffy: 'fuzzy' Bau
        Bau: 'bau' BauBau
        """;
        var gram = getView(src);
        var binder = new Binder();
        binder.RegisterRules(gram.Rules);
        binder.PopulateRules();
        binder.CreateTypes();

        Assert.Throws<CompilationException>(binder.InspectRules);
    }

    private static GrammarView getView(string src)
    {
        var tokenizer = new Tokenizer(SynchronizationPoint.ClearPoint(new StringBuffer(src + '\n')), saveTrivia: true);
        var parser = new GrammarParser(new TokenNodeStream(tokenizer));
        var node = parser.Parse();
        Debug.Assert(node is not null, "Given syntax is not valid.");
        return node.GetView(TokenPosition.StartOfFile, null);
    }
}
