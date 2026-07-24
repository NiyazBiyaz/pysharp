using System.Diagnostics;
using PySharp.SyntaxAnalysis.Tokens;
using static PySharp.SyntaxAnalysis.Tokens.TokenType;

namespace PySharp.Tests.SyntaxAnalysis.Tokens;

public class TestTokenizer
{
    private static readonly Dictionary<string, (string code, IList<Token> expected)> one_row_test_cases =
        new()
        {
            ["String_SimpleSingle"] = ("'123'", [new(StringLiteral, "'123'"), eof]),
            ["String_SimpleDouble"] = ("\"123\"", [new(StringLiteral, "\"123\""), eof]),
            ["String_SpaceBoth"] = ("'123' \"123\"", [new(StringLiteral, "'123'"),
                                                      new(WhiteSpace, " "),
                                                      new(StringLiteral, "\"123\""), eof]),
            ["String_Empty"] = ("''", [new(StringLiteral, "''"), eof]),
            ["String_raw"] = ("r'123'", [new(StringLiteral, "r'123'"), eof]),
            ["String_RAW"] = ("R'123'", [new(StringLiteral, "R'123'"), eof]),
            ["String_byte"] = ("b'123'", [new(StringLiteral, "b'123'"), eof]),
            ["String_BYTE"] = ("B'123'", [new(StringLiteral, "B'123'"), eof]),
            ["String_RawByte"] = ("rb'123'", [new(StringLiteral, "rb'123'"), eof]),
            ["String_ByteRaw"] = ("br'123'", [new(StringLiteral, "br'123'"), eof]),
            ["String_EscapedQuote"] = (@"'bau\'bau'", [new(StringLiteral, @"'bau\'bau'"), eof]),

            ["Name_Simple"] = ("bau", [new(Name, "bau"), eof]),
            ["Name_Space"] = ("bau bau", [new(Name, "bau"), new(WhiteSpace, " "), new(Name, "bau"), eof]),
            ["Name_TrailingUnderscore"] = ("baubau__", [new(Name, "baubau__"), eof]),
            ["Name_LeadingUnderscore"] = ("__bau", [new(Name, "__bau"), eof]),
            ["Name_BetweenUnderscore"] = ("__bau__", [new(Name, "__bau__"), eof]),
            ["Name_WithDigits"] = ("b123", [new(Name, "b123"), eof]),
            ["Name_Complex"] = ("bau_123_bau__", [new(Name, "bau_123_bau__"), eof]),

            ["Number_Simple"] = ("123", [new(Number, "123"), eof]),
            ["Number_Space"] = ("10 10", [new(Number, "10"), new(WhiteSpace, " "), new(Number, "10"), eof]),
            ["Number_Zeros"] = ("000", [new(Number, "000"), eof]),
            ["Number_FractionZeros"] = ("0.0", [new(Number, "0.0"), eof]),
            ["Number_FractionLeadingZeros"] = ("01.10", [new(Number, "01.10"), eof]),
            ["Number_FractionLeadingZerosDot"] = ("01.", [new(Number, "01."), eof]),
            ["Number_LeadingDot"] = (".10", [new(Number, ".10"), eof]),
            ["Number_TrailingDot"] = ("10.", [new(Number, "10."), eof]),
            ["Number_SimpleExponent"] = ("10e2", [new(Number, "10e2"), eof]),
            ["Number_PlusExponent"] = ("10e+2", [new(Number, "10e+2"), eof]),
            ["Number_MinusExponent"] = ("10e-2", [new(Number, "10e-2"), eof]),
            ["Number_DotExponent"] = ("10.e2", [new(Number, "10.e2"), eof]),
            ["Number_SimpleImaginary"] = ("10j", [new(Number, "10j"), eof]),
            ["Number_ImaginaryAfterDot"] = ("10.j", [new(Number, "10.j"), eof]),
            ["Number_ImaginaryAfterExpo"] = ("10e1j", [new(Number, "10e1j"), eof]),
            ["Number_ImaginaryAfterDotExpo"] = ("10.e1j", [new(Number, "10.e1j"), eof]),
            ["Number_NormalUnderscores"] = ("1_000_000", [new(Number, "1_000_000"), eof]),
            ["Number_WildUnderscores"] = ("10_0_0_0_0", [new(Number, "10_0_0_0_0"), eof]),
            ["Number_FractionUnderscores"] = ("1_1.1_1", [new(Number, "1_1.1_1"), eof]),
            ["Number_Complex1"] = ("1_2_3.1_2_3e+2j", [new(Number, "1_2_3.1_2_3e+2j"), eof]),
            ["Number_Complex2"] = (".1_2_3e-2j", [new(Number, ".1_2_3e-2j"), eof]),
            ["Number_Complex3"] = ("1_2_3.1_2_3e+2_0j", [new(Number, "1_2_3.1_2_3e+2_0j"), eof]),

            ["Number_Hexadecimal"] = ("0x123", [new(Number, "0x123"), eof]),
            ["Number_HexadecimalFullLower"] = ("0x0123456789abcdef", [new(Number, "0x0123456789abcdef"), eof]),
            ["Number_HexadecimalFullUpper"] = ("0x0123456789ABCDEF", [new(Number, "0x0123456789ABCDEF"), eof]),
            ["Number_HexadecimalMixed"] = ("0xAbCdEf", [new(Number, "0xAbCdEf"), eof]),
            ["Number_HexadecimalUnderscores"] = ("0x33_22_11", [new(Number, "0x33_22_11"), eof]),
            ["Number_HexadecimalLeadUnderscores"] = ("0x_33_22_11", [new(Number, "0x_33_22_11"), eof]),
            ["Number_OctalFull"] = ("0o01234567", [new(Number, "0o01234567"), eof]),
            ["Number_OctalUnderscores"] = ("0o_123_456_7", [new(Number, "0o_123_456_7"), eof]),
            ["Number_BinaryFull"] = ("0b01", [new(Number, "0b01"), eof]),
            ["Number_BinaryUnderscores"] = ("0b_1_0", [new(Number, "0b_1_0"), eof]),
            ["Number_BinaryLong"] = ("0b101010100100100010101001", [new(Number, "0b101010100100100010101001"), eof]),

#pragma warning disable format
            ["Op_Comma"]            = (",",   [Comma.GetStandardToken(), eof]),
            ["Op_Colon"]            = (":",   [Colon.GetStandardToken(), eof]),
            ["Op_ColonEqual"]       = (":=",  [ColonEqual.GetStandardToken(), eof]),
            ["Op_Semicolon"]        = (";",   [Semicolon.GetStandardToken(), eof]),
            ["Op_Equal"]            = ("=",   [Equal.GetStandardToken(), eof]),
            ["Op_EqEqual"]          = ("==",  [EqEqual.GetStandardToken(), eof]),
            ["Op_Plus"]             = ("+",   [Plus.GetStandardToken(), eof]),
            ["Op_PlusEqual"]        = ("+=",  [PlusEqual.GetStandardToken(), eof]),
            ["Op_Minus"]            = ("-",   [Minus.GetStandardToken(), eof]),
            ["Op_MinusEqual"]       = ("-=",  [MinusEqual.GetStandardToken(), eof]),
            ["Op_RightArrow"]       = ("->",  [RightArrow.GetStandardToken(), eof]),
            ["Op_Star"]             = ("*",   [Star.GetStandardToken(), eof]),
            ["Op_StarEqual"]        = ("*=",  [StarEqual.GetStandardToken(), eof]),
            ["Op_DoubleStar"]       = ("**",  [DoubleStar.GetStandardToken(), eof]),
            ["Op_DoubleStarEqual"]  = ("**=", [DoubleStarEqual.GetStandardToken(), eof]),
            ["Op_Slash"]            = ("/",   [Slash.GetStandardToken(), eof]),
            ["Op_SlashEqual"]       = ("/=",  [SlashEqual.GetStandardToken(), eof]),
            ["Op_DoubleSlash"]      = ("//",  [DoubleSlash.GetStandardToken(), eof]),
            ["Op_DoubleSlashEqual"] = ("//=", [DoubleSlashEqual.GetStandardToken(), eof]),
            ["Op_Percent"]          = ("%",   [Percent.GetStandardToken(), eof]),
            ["Op_PercentEqual"]     = ("%=",  [PercentEqual.GetStandardToken(), eof]),
            ["Op_Ampersand"]        = ("&",   [Ampersand.GetStandardToken(), eof]),
            ["Op_AmpersandEqual"]   = ("&=",  [AmpersandEqual.GetStandardToken(), eof]),
            ["Op_VertBar"]          = ("|",   [VertBar.GetStandardToken(), eof]),
            ["Op_VertBarEqual"]     = ("|=",  [VertBarEqual.GetStandardToken(), eof]),
            ["Op_At"]               = ("@",   [At.GetStandardToken(), eof]),
            ["Op_AtEqual"]          = ("@=",  [AtEqual.GetStandardToken(), eof]),
            ["Op_Circumflex"]       = ("^",   [Circumflex.GetStandardToken(), eof]),
            ["Op_CircumflexEqual"]  = ("^=",  [CircumflexEqual.GetStandardToken(), eof]),
            ["Op_Tilde"]            = ("~",   [Tilde.GetStandardToken(), eof]),
            ["Op_Greater"]          = (">",   [Greater.GetStandardToken(), eof]),
            ["Op_GreaterEqual"]     = (">=",  [GreaterEqual.GetStandardToken(), eof]),
            ["Op_RightShift"]       = (">>",  [RightShift.GetStandardToken(), eof]),
            ["Op_RightShiftEqual"]  = (">>=", [RightShiftEqual.GetStandardToken(), eof]),
            ["Op_Less"]             = ("<",   [Less.GetStandardToken(), eof]),
            ["Op_LessEqual"]        = ("<=",  [LessEqual.GetStandardToken(), eof]),
            ["Op_LeftShift"]        = ("<<",  [LeftShift.GetStandardToken(), eof]),
            ["Op_LeftShiftEqual"]   = ("<<=", [LeftShiftEqual.GetStandardToken(), eof]),
            ["Op_Exclamation"]      = ("!",   [Exclamation.GetStandardToken(), eof]),
            ["Op_NotEqual"]         = ("!=",  [NotEqual.GetStandardToken(), eof]),
#pragma warning restore format
            // Since dots related to numbers they're separated.
            ["Op_Dot"] = (".", [new(Dot, "."), eof]),
            ["Op_DotSpace"] = (". .", [new(Dot, "."), new(WhiteSpace, " "), new(Dot, "."), eof]),
            ["Op_Ellipsis"] = ("...", [new(Ellipsis, "..."), eof]),
            ["Op_EllipsisSpace"] = ("... ...", [new(Ellipsis, "..."), new(WhiteSpace, " "), new(Ellipsis, "..."), eof]),
            ["Op_EllipsisDot"] = ("... .", [new(Ellipsis, "..."), new(WhiteSpace, " "), new(Dot, "."), eof]),
        };

    [Theory]
    // Names
    [InlineData("Name_Simple")]
    [InlineData("Name_Space")]
    [InlineData("Name_TrailingUnderscore")]
    [InlineData("Name_LeadingUnderscore")]
    [InlineData("Name_BetweenUnderscore")]
    [InlineData("Name_WithDigits")]
    [InlineData("Name_Complex")]
    // Numbers
    [InlineData("Number_Simple")]
    [InlineData("Number_Space")]
    [InlineData("Number_Zeros")]
    [InlineData("Number_FractionZeros")]
    [InlineData("Number_FractionLeadingZeros")]
    [InlineData("Number_FractionLeadingZerosDot")]
    [InlineData("Number_LeadingDot")]
    [InlineData("Number_TrailingDot")]
    [InlineData("Number_SimpleExponent")]
    [InlineData("Number_PlusExponent")]
    [InlineData("Number_MinusExponent")]
    [InlineData("Number_DotExponent")]
    [InlineData("Number_SimpleImaginary")]
    [InlineData("Number_ImaginaryAfterDot")]
    [InlineData("Number_ImaginaryAfterExpo")]
    [InlineData("Number_ImaginaryAfterDotExpo")]
    [InlineData("Number_NormalUnderscores")]
    [InlineData("Number_WildUnderscores")]
    [InlineData("Number_FractionUnderscores")]
    [InlineData("Number_Complex1")]
    [InlineData("Number_Complex2")]
    [InlineData("Number_Complex3")]
    [InlineData("Number_Hexadecimal")]
    [InlineData("Number_HexadecimalFullLower")]
    [InlineData("Number_HexadecimalFullUpper")]
    [InlineData("Number_HexadecimalMixed")]
    [InlineData("Number_HexadecimalUnderscores")]
    [InlineData("Number_HexadecimalLeadUnderscores")]
    [InlineData("Number_OctalFull")]
    [InlineData("Number_OctalUnderscores")]
    [InlineData("Number_BinaryFull")]
    [InlineData("Number_BinaryUnderscores")]
    [InlineData("Number_BinaryLong")]
    // Operators
    [InlineData("Op_Comma")]
    [InlineData("Op_Colon")]
    [InlineData("Op_ColonEqual")]
    [InlineData("Op_Semicolon")]
    [InlineData("Op_Equal")]
    [InlineData("Op_EqEqual")]
    [InlineData("Op_Plus")]
    [InlineData("Op_PlusEqual")]
    [InlineData("Op_Minus")]
    [InlineData("Op_MinusEqual")]
    [InlineData("Op_RightArrow")]
    [InlineData("Op_Star")]
    [InlineData("Op_StarEqual")]
    [InlineData("Op_DoubleStar")]
    [InlineData("Op_DoubleStarEqual")]
    [InlineData("Op_Slash")]
    [InlineData("Op_SlashEqual")]
    [InlineData("Op_DoubleSlash")]
    [InlineData("Op_DoubleSlashEqual")]
    [InlineData("Op_Percent")]
    [InlineData("Op_PercentEqual")]
    [InlineData("Op_Ampersand")]
    [InlineData("Op_AmpersandEqual")]
    [InlineData("Op_VertBar")]
    [InlineData("Op_VertBarEqual")]
    [InlineData("Op_At")]
    [InlineData("Op_AtEqual")]
    [InlineData("Op_Circumflex")]
    [InlineData("Op_CircumflexEqual")]
    [InlineData("Op_Tilde")]
    [InlineData("Op_Greater")]
    [InlineData("Op_GreaterEqual")]
    [InlineData("Op_RightShift")]
    [InlineData("Op_RightShiftEqual")]
    [InlineData("Op_Less")]
    [InlineData("Op_LessEqual")]
    [InlineData("Op_LeftShift")]
    [InlineData("Op_LeftShiftEqual")]
    [InlineData("Op_Exclamation")]
    [InlineData("Op_NotEqual")]
    [InlineData("Op_Dot")]
    [InlineData("Op_DotSpace")]
    [InlineData("Op_Ellipsis")]
    [InlineData("Op_EllipsisSpace")]
    [InlineData("Op_EllipsisDot")]
    // One-row strings.
    [InlineData("String_SimpleSingle")]
    [InlineData("String_SimpleDouble")]
    [InlineData("String_SpaceBoth")]
    [InlineData("String_Empty")]
    [InlineData("String_raw")]
    [InlineData("String_RAW")]
    [InlineData("String_byte")]
    [InlineData("String_BYTE")]
    [InlineData("String_RawByte")]
    [InlineData("String_ByteRaw")]
    [InlineData("String_EscapedQuote")]
    public void TestOneRow(string @case)
    {
        Debug.Assert(one_row_test_cases.ContainsKey(@case));

        (string code, var expected) = one_row_test_cases[@case];

        test(code, expected);
    }

    private static readonly Dictionary<string, (string code, IList<Token> tokens)> multiline_string_test_cases =
        new()
        {
            ["InOneLine"] = ("""
            '''bau'''
            """, [new(StringLiteral, "'''bau'''"), eof]),

            ["MixedQuotes"] = ("''' \"\"\" '''", [new(StringLiteral, "''' \"\"\" '''"), eof]),

            ["EscapedQuote"] = ("""
            ''' \' '''
            """,

            [new(StringLiteral, "''' \\' '''"), eof]),

            ["OneLineFeed"] = ("""
            '''bau
            bau'''
            """, [new(StringLiteral, "'''bau\nbau'''"), eof]),

            ["EscapedEndOfLine"] = ("""
            '''bau\
            bau'''
            """, [new(StringLiteral, "'''bau\\\nbau'''"), eof]),

            ["LineFeedCRLF"] = ("'''bau\r\nbau'''", [new(StringLiteral, "'''bau\r\nbau'''"), eof]),

            ["LineFeedCR"] = ("'''bau\rbau'''", [new(StringLiteral, "'''bau\rbau'''"), eof]),

            ["Empty"] = ("''''''", [new(StringLiteral, "''''''"), eof]),

            ["Raw"] = (@"r'''b\au'''", [new(StringLiteral, @"r'''b\au'''"), eof]),

            ["Byte"] = (@"b'''r\au'''", [new(StringLiteral, @"b'''r\au'''"), eof]),

            ["ByteRaw"] = (@"rb'''me\ow'''", [new(StringLiteral, @"rb'''me\ow'''"), eof]),

            ["LongString"] = ("""
            '''bau1"
            bau2'
            bau3
            bau4'
            '''
            """,
            [new(StringLiteral, "'''bau1\"\nbau2'\nbau3\nbau4'\n'''"), eof]),
        };

    [Theory]
    [InlineData("InOneLine")]
    [InlineData("MixedQuotes")]
    [InlineData("EscapedQuote")]
    [InlineData("OneLineFeed")]
    [InlineData("EscapedEndOfLine")]
    [InlineData("LineFeedCRLF")]
    [InlineData("LineFeedCR")]
    [InlineData("Empty")]
    [InlineData("Raw")]
    [InlineData("Byte")]
    [InlineData("ByteRaw")]
    [InlineData("LongString")]
    public void TestMultilineString(string @case)
    {
        Debug.Assert(multiline_string_test_cases.ContainsKey(@case));

        (string code, var expected) = multiline_string_test_cases[@case];

        test(code, expected);
    }

    private static readonly Dictionary<string, (string code, IList<Token> expected)> parens_test_cases =
        new()
        {
            ["AllInRow"] = ("({[]})", [
                new(LeftParen, "("),
                new(LeftBrace, "{"),
                new(LeftSquareBracket, "["),
                new(RightSquareBracket, "]"),
                new(RightBrace, "}"),
                new(RightParen, ")"),
                eof,
            ]),
            ["Paren"] = ("(\n)", [
                new(LeftParen, "("),
                new(TriviaNewLine, "\n"),
                new(RightParen, ")"),
                eof,
            ]),
            ["Brace"] = ("{\n}", [
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(RightBrace, "}"),
                eof,
            ]),
            ["SqBrt"] = ("[\n]", [
                new(LeftSquareBracket, "["),
                new(TriviaNewLine, "\n"),
                new(RightSquareBracket, "]"),
                eof,
            ]),
            ["ReducingNesting"] = ("""
            (
            [
            ]
            )
            """,
            [
                new(LeftParen, "("), new(TriviaNewLine, "\n"),
                new(LeftSquareBracket, "["), new(TriviaNewLine, "\n"),
                new(RightSquareBracket, "]"), new(TriviaNewLine, "\n"),
                new(RightParen, ")"),
                eof,
            ]),
            ["RestoreLogicalNewLines"] = ("""
            (
            [
            ]
            )
            bau
            """,
            [
                new(LeftParen, "("), new(TriviaNewLine, "\n"),
                new(LeftSquareBracket, "["), new(TriviaNewLine, "\n"),
                new(RightSquareBracket, "]"), new(TriviaNewLine, "\n"),
                new(RightParen, ")"), new(NewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["NoIndents"] = ("""
            (
                bau
            )
            """, [
                new(LeftParen, "("), new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(TriviaNewLine, "\n"),
                new(RightParen, ")"),
                eof,
            ]),
        };

    [Theory]
    [InlineData("AllInRow")]
    [InlineData("Paren")]
    [InlineData("Brace")]
    [InlineData("SqBrt")]
    [InlineData("ReducingNesting")]
    [InlineData("RestoreLogicalNewLines")]
    [InlineData("NoIndents")]
    public void TestBrackets(string @case)
    {
        Debug.Assert(parens_test_cases.ContainsKey(@case));

        (string code, var expected) = parens_test_cases[@case];

        test(code, expected);
    }

    private static readonly Dictionary<string, (string code, IList<Token> expected)> indentation_test_cases =
        new()
        {
            ["OneIndent"] = ("""
            bau
                bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(Dedent, empty),
                eof,
            ]),
            ["TwoIndents"] = ("""
            bau
                bau
                    bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"),
                new(Dedent, empty), new(Dedent, empty),
                eof,
            ]),
            ["OneDedent"] = ("""
            bau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["TwoDedentSoft"] = ("""
            bau
                bau
                    bau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["TwoDedentHard"] = ("""
            bau
                bau
                    bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["IndentAfterDedent"] = ("""
            bau
                bau
                    bau
                bau
                    bau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        " /* Re-indent will have all chars from start to valued token */),
                new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["HoldingIndentation"] = ("""
            bau
                bau
                bau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["HoldingIndentationWithSpace"] = ("""
            bau
                bau

                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["HoldingIndentationWithComment"] = ("""
            bau
                bau
                # baubau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "    "), new(Comment, "# baubau"), new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["HoldingIndentationWithMoreNestedComment"] = ("""
            bau
                bau
                    # baubau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "        "), new(Comment, "# baubau"), new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["HoldingIndentationWithLessNestedComment"] = ("""
            bau
                bau
            # baubau
                bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Comment, "# baubau"), new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["TabIndents"] = ("bau\n\tbau",
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "\t"), new(Name, "bau"),
                new(Dedent, empty), eof,
            ]),
            // No tests with form-feed, because in Python reference it's marked as UB
            // but they're supported and interprets as spaces.
            ["BigIndents"] = ("""
            bau
                    bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["MixedIndents"] = ("bau\n\t  bau\nbau",
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "\t  "), new(Name, "bau"), new(NewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof,
            ]),
            ["Continuation_IndentDoesNotChanges"] = ("""
            bau\
                bau

                bau\
            bau
            bau
            """,
            [
            new(Name, "bau"), BackSlash.GetStandardToken(), new(TriviaNewLine, "\n"),
            new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
            new(TriviaNewLine, "\n"),
            new(Indent, "    "), new(Name, "bau"), BackSlash.GetStandardToken(), new(TriviaNewLine, "\n"),
            new(Name, "bau"), new(NewLine, "\n"),
            new(Dedent, empty), new(Name, "bau"),
            eof,
            ]),
            ["Continuation_NewLineDoesNotGenerates"] = ("""
            bau\
            \
            \
            bau
            """,
            [
                new(Name, "bau"), BackSlash.GetStandardToken(), new(TriviaNewLine, "\n"),
                BackSlash.GetStandardToken(), new(TriviaNewLine, "\n"),
                BackSlash.GetStandardToken(), new(TriviaNewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["ContinuationTrivia_IndentDoesNotChanges"] = ("""
            bau\
                bau

                bau\
            bau
            bau
            """,
            [
            new(Name, "bau"), new(BackSlash, "\\"), new(TriviaNewLine, "\n"),
            new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
            new(TriviaNewLine, "\n"),
            new(Indent, "    "), new(Name, "bau"), new(BackSlash, "\\"),
            new(TriviaNewLine, "\n"),
            new(Name, "bau"), new(NewLine, "\n"),
            new(Dedent, empty), new(Name, "bau"),
            eof,
            ]),
            ["ContinuationTrivia_NewLineDoesNotGenerates"] = ("""
            bau\
            \
            \
            bau
            """,
            [
                new(Name, "bau"), new(BackSlash, "\\"), new(TriviaNewLine, "\n"),
                new(BackSlash, "\\"), new(TriviaNewLine, "\n"),
                new(BackSlash, "\\"), new(TriviaNewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["LineFeed_LF"] = ("bau\nbau",
            [
                new(Name, "bau"),
                new(NewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["LineFeed_CR"] = ("bau\rbau",
            [
                new(Name, "bau"),
                new(NewLine, "\r"),
                new(Name, "bau"),
                eof,
            ]),
            ["LineFeed_CRLF"] = ("bau\r\nbau",
            [
                new(Name, "bau"),
                new(NewLine, "\r\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["CommentsTrivia"] = ("""
            bau # bau bau bau
            bau # bababau
            bau # bau~ bau~
            # bau
            bau
            """,
            [
                new(Name, "bau"), new(WhiteSpace, " "), new(Comment, "# bau bau bau"), new(NewLine, "\n"),
                new(Name, "bau"), new(WhiteSpace, " "), new(Comment, "# bababau"), new(NewLine, "\n"),
                new(Name, "bau"), new(WhiteSpace, " "), new(Comment, "# bau~ bau~"), new(NewLine, "\n"),
                new(Comment, "# bau"), new(TriviaNewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["SaveTriviaInIndent"] = ("""
            bau
                bau
                bau
                # bau
            bau
            """,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(WhiteSpace, "    "), new(Comment, "# bau"), new(TriviaNewLine, "\n"),
                new(Dedent, empty), new(Name, "bau"),
                eof
            ])
        };

    [Theory]
    [InlineData("OneIndent")]
    [InlineData("TwoIndents")]
    [InlineData("OneDedent")]
    [InlineData("TwoDedentSoft")]
    [InlineData("TwoDedentHard")]
    [InlineData("IndentAfterDedent")]
    [InlineData("HoldingIndentation")]
    [InlineData("HoldingIndentationWithSpace")]
    [InlineData("HoldingIndentationWithComment")]
    [InlineData("HoldingIndentationWithMoreNestedComment")]
    [InlineData("HoldingIndentationWithLessNestedComment")]
    [InlineData("TabIndents")]
    [InlineData("BigIndents")]
    [InlineData("MixedIndents")]
    [InlineData("Continuation_IndentDoesNotChanges")]
    [InlineData("Continuation_NewLineDoesNotGenerates")]
    [InlineData("ContinuationTrivia_IndentDoesNotChanges")]
    [InlineData("ContinuationTrivia_NewLineDoesNotGenerates")]
    [InlineData("LineFeed_LF")]
    [InlineData("LineFeed_CR")]
    [InlineData("LineFeed_CRLF")]
    [InlineData("CommentsTrivia")]
    [InlineData("SaveTriviaInIndent")]
    public void TestIndentation(string @case)
    {
        Debug.Assert(indentation_test_cases.ContainsKey(@case));

        (string code, var expected) = indentation_test_cases[@case];

        test(code, expected);
    }

    private const string dec_inv = "Invalid decimal literal.";
    private const string img_inv = "Invalid imaginary literal.";
    private const string bin_inv = "Invalid binary literal.";
    private const string hex_inv = "Invalid hexadecimal literal.";
    private const string oct_inv = "Invalid octal literal.";
    private const string str_prf = "'{0}' and '{1}' prefixes are incompatible.";
    private const string unterminated = "Unterminated string literal.";

    private static readonly Dictionary<string, (string code, string message, IList<Token> expected)> literal_errors_test_cases =
        new()
        {
            ["Number_StopEatingInvalidOnNonAsciiLetter"] =
            ("12baubau+bau", dec_inv,
            [
                new(Error, "12baubau"),
                new(Plus, "+"),
                new(Name, "bau"),
                eof,
            ]),
            // Valid token after error for make sure that error recovery works fine.
            ["Number_DoubleUnderscore_Integer"] =
            ("123__123 bau", dec_inv,
            [
                new(Error, "123__123"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_TrailingUnderscore_Integer"] =
            ("1231234_ bau", dec_inv,
            [
                new(Error, "1231234_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidTrailingChar_Integer"] =
            ("1231234i bau", dec_inv,
             [
                new(Error, "1231234i"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidChar_Integer"] =
            ("123bb123 bau", dec_inv,
            [
                new(Error, "123bb123"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_DoubleUnderscore_HexInt"] =
            ("0xff__ff bau", hex_inv,
            [
                new(Error, "0xff__ff"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_TrailingUnderscore_HexInt"] =
            ("0xfafa0_ bau", hex_inv,
            [
                new(Error, "0xfafa0_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidTrailingChar_HexInt"] =
            ("0xfaFa0h bau", hex_inv,
            [
                new(Error, "0xfaFa0h"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidChar_HexInt"] =
            ("0xfggFa0 bau", hex_inv,
            [
                new(Error, "0xfggFa0"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_DoubleUnderscore_OctInt"] =
            ("0o77__33 bau", oct_inv,
            [
                new(Error, "0o77__33"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_TrailingUnderscore_OctInt"] =
            ("0o12333_ bau", oct_inv,
            [
                new(Error, "0o12333_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidTrailingChar_OctInt"] =
            ("0o12312e bau", oct_inv,
            [
                new(Error, "0o12312e"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidChar_OctInt"] =
            ("0o138813 bau", oct_inv,
            [
                new(Error, "0o138813"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_LeadingZerosInInteger"] =
            ("00001234 bau", "Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.",
            [
                new(Error, "00001234"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_DoubleUnderscore_BinInt"] =
            ("0b11__00 bau", bin_inv,
            [
                new(Error, "0b11__00"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_TrailingUnderscore_BinInt"] =
            ("0b10101_ bau", bin_inv,
            [
                new(Error, "0b10101_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidTrailingChar_BinInt"] =
            ("0b10101e bau", bin_inv,
            [
                new(Error, "0b10101e"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_InvalidChar_BinInt"] =
            ("0b101020 bau", bin_inv,
            [
                new(Error, "0b101020"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            // Float
            ["Number_Float_DoubleUnderscore"] =
            ("12.12__1 bau", dec_inv,
            [
                new(Error, "12.12__1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreBeforeDot"] =
            ("123_.123 bau", dec_inv,
            [
                new(Error, "123_.123"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_TrailingUnderscore"] =
            ("123.123_ bau", dec_inv,
            [
                new(Error, "123.123_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_InvalidChar"] =
            ("12a.1a23 bau", dec_inv,
            [
                new(Error, "12a.1a23"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_InvalidTrailingChar"] =
            ("123.123a bau", dec_inv,
            [
                new(Error, "123.123a"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_EmptyAfterE"] =
            ("123.233e bau", dec_inv,
            [
                new(Error, "123.233e"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreBeforeE"] =
            ("13.23_e1 bau", dec_inv,
            [
                new(Error, "13.23_e1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreAfterE"] =
            ("13.23e_1 bau", dec_inv,
            [
                new(Error, "13.23e_1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreBeforePlus"] =
            ("1.23e_+1 bau", dec_inv,
            [
                new(Error, "1.23e_+1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreAfterPlus"] =
            ("1.23e+_1 bau", dec_inv,
            [
                new(Error, "1.23e+_1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreBeforeMinus"] =
            ("1.23e_-1 bau", dec_inv,
            [
                new(Error, "1.23e_-1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Float_UnderscoreAfterMinus"] =
            ("1.23e-_1 bau", dec_inv,
            [
                new(Error, "1.23e-_1"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Imaginary_UnderscoreBeforeJ"] =
            ("123.13_j bau", img_inv,
            [
                new(Error, "123.13_j"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Imaginary_UnderscoreAfterJ"] =
            ("123.13j_ bau", img_inv,
            [
                new(Error, "123.13j_"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Imaginary_InvalidCharBeforeJ"] =
            ("123.13fj bau", img_inv,
            [
                new(Error, "123.13fj"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["Number_Imaginary_InvalidCharAfterJ"] =
            ("123.13jf bau", img_inv,
            [
                new(Error, "123.13jf"),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            // Strings (F-/T-strings are separated)
            ["String_PrefixUR"] =
            ("ur\"bBau\" bau", string.Format(str_prf, "r", "u"),
            [
                new(Error, "ur\"bBau\""),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["String_PrefixUB"] =
            ("ub\"bBau\" bau", string.Format(str_prf, "b", "u"),
            [
                new(Error, "ub\"bBau\""),
                new(WhiteSpace, " "),
                new(Name, "bau"),
                eof,
            ]),
            ["String_UnclosedOnLine_SingleQuote"] =
            // After unclosed string don't eat new line character
            ("\"baubau\nbau", unterminated,
            [
                new(Error, "\"baubau"), new(NewLine, "\n"),
                new(Name, "bau"),
                eof,
            ]),
            ["String_UnclosedOnFile_SingleQuote"] =
            ("\"baubau", unterminated,
            [
                new(Error, "\"baubau"),
                eof,
            ]),
            ["String_UnclosedOnFile_TripleQuote_SingleLine"] =
            ("'''baubaubaubaubau", unterminated,
            [
                new(Error, "'''baubaubaubaubau"),
                eof,
            ]),
            ["String_UnclosedOnFile_TripleQuote_MultiLine"] =
            ("'''bauba\nbauba\nbau", unterminated,
            [
                new(Error, "'''bauba\nbauba\nbau"),
                eof,
            ]),
            ["String_UnclosedOnFile_EscapedQuote"] =
            // `"bau\"bau`
            ("\"bau\\\"bau", "Unterminated string literal. Perhaps you escaped the end quote?",
            [
                new(Error, "\"bau\\\"bau"),
                eof,
            ]),

            ["Partial_SingleQuoteUnterminatedEOF"] =
            ("f'abc", unterminated,
            [
                new(FStringStart, "f'"),
                new(Error, "abc"),
                eof
            ]),

            ["Partial_SingleQuoteUnterminatedNewline"] =
            ("f'abc\n", unterminated,
            [
                new(FStringStart, "f'"),
                new(Error, "abc"),
                new(NewLine, "\n"),
                eof
            ]),
            ["Partial_TripleQuoteUnterminatedEOF"] =
            ("f'''content", unterminated,
            [
                new(FStringStart, "f'''"),
                new(Error, "content"),
                eof
            ]),
            ["Partial_InterpolationUnclosedBraceEOF"] =
            ("f'{expr", "Unexpected EOF in multi-line statement.",
            [
                new(FStringStart, "f'"),
                new(LeftBrace, "{"),
                new(Name, "expr"),
                new(Error, ""),
                eof
            ]),
            ["Partial_InterpolationLineLimitExceeded_Fatal"] =
            ("""
            f'{b
            a
            u
            b
            a
            u}'
            """, "Interpolation exceeds maximum line limit. Allowed maximum 4 lines.",
            [
                new(FStringStart, "f'"),
                new(LeftBrace, "{"),
                new(Name, "b"), new(TriviaNewLine, "\n"),
                new(Name, "a"), new(TriviaNewLine, "\n"),
                new(Name, "u"), new(TriviaNewLine, "\n"),
                new(Name, "b"), new(TriviaNewLine, "\n"),
                new(Name, "a"), new(TriviaNewLine, "\n"),
                new(Error, "u}'"),
                eof
            ]),
            ["Partial_NestedUnterminatedString(Only one error expecting)"] =
            ("f'{x'", unterminated,
            [
                new(FStringStart, "f'"),
                new(LeftBrace, "{"),
                new(Name, "x"),
                new(Error, "'"),
                eof
            ]),
            ["Partial_NestingDepthExceeded_Fatal"] =
            ("f'1{f'2{f'3{f'4{f'5{f'6'}'}'}'}'}'", "f-string: nesting depth exceeded (limit: 5).",
            [
                new(FStringStart, "f'"),
                new(FStringMiddle, "1"),
                new(LeftBrace, "{"),
                new(FStringStart, "f'"),
                new(FStringMiddle, "2"),
                new(LeftBrace, "{"),
                new(FStringStart, "f'"),
                new(FStringMiddle, "3"),
                new(LeftBrace, "{"),
                new(FStringStart, "f'"),
                new(FStringMiddle, "4"),
                new(LeftBrace, "{"),
                new(FStringStart, "f'"),
                new(FStringMiddle, "5"),
                new(LeftBrace, "{"),
                new(Error, "f'6'}'}'}'}'}'"),
                eof
            ]),
            ["Partial_FormatSpecNestedInterpolationLineLimit_Fatal"] =
            ("""
            f'{x:




            {y}}'
            """, "Interpolation exceeds maximum line limit. Allowed maximum 4 lines.",
            [
                new(FStringStart, "f'"),
                new(LeftBrace, "{"),
                new(Name, "x"),
                new(Colon, ":"),
                new(FStringMiddle, "\n\n\n\n\n"),
                new(Error, "{y}}'"),
                eof
            ]),

            ["Partial_InvalidPrefix_bf"] =
            ("bf'hello'", string.Format(str_prf, "b", "f"),
            [
                new(Error, "bf"),
                new(StringLiteral, "'hello'"),
                eof
            ]),

            ["Partial_InvalidPrefix_fb"] =
            ("fb'h{1}o'", string.Format(str_prf, "b", "f"),
            [
                new(Error, "fb"),
                new(StringLiteral, "'h{1}o'"),
                eof
            ]),

            ["Partial_InvalidPrefix_bt"] =
            ("bt'''hello'''", string.Format(str_prf, "b", "t"),
            [
                new(Error, "bt"),
                new(StringLiteral, "'''hello'''"),
                eof
            ]),

            ["Partial_InvalidPrefix_tb"] =
            ("tb'''hello'''", string.Format(str_prf, "b", "t"),
            [
                new(Error, "tb"),
                new(StringLiteral, "'''hello'''"),
                eof
            ]),

            ["Partial_InvalidPrefix_ft"] =
            ("ft'h{1}o'", string.Format(str_prf, "f", "t"),
            [
                new(Error, "ft"),
                new(StringLiteral, "'h{1}o'"),
                eof
            ]),

            ["Partial_InvalidPrefix_tf"] =
            ("tf'''hello'''", string.Format(str_prf, "f", "t"),
            [
                new(Error, "tf"),
                new(StringLiteral, "'''hello'''"),
                eof
            ]),
            ["Partial_InvalidShieldedBrace"] =
            ("f' } '", "Use double curly brackets '}}' to shield it in interpolated string.",
            [
                new(FStringStart, "f'"),
                new(FStringMiddle, " "),
                new(Error, "}"),
                new(FStringMiddle, " "),
                new(FStringEnd, "'"),
                eof
            ])
        };

    [Theory]
    [InlineData("Number_StopEatingInvalidOnNonAsciiLetter")]
    [InlineData("Number_DoubleUnderscore_Integer")]
    [InlineData("Number_TrailingUnderscore_Integer")]
    [InlineData("Number_InvalidTrailingChar_Integer")]
    [InlineData("Number_InvalidChar_Integer")]
    [InlineData("Number_DoubleUnderscore_HexInt")]
    [InlineData("Number_TrailingUnderscore_HexInt")]
    [InlineData("Number_InvalidTrailingChar_HexInt")]
    [InlineData("Number_InvalidChar_HexInt")]
    [InlineData("Number_DoubleUnderscore_OctInt")]
    [InlineData("Number_TrailingUnderscore_OctInt")]
    [InlineData("Number_InvalidTrailingChar_OctInt")]
    [InlineData("Number_InvalidChar_OctInt")]
    [InlineData("Number_LeadingZerosInInteger")]
    [InlineData("Number_DoubleUnderscore_BinInt")]
    [InlineData("Number_TrailingUnderscore_BinInt")]
    [InlineData("Number_InvalidTrailingChar_BinInt")]
    [InlineData("Number_InvalidChar_BinInt")]
    [InlineData("Number_Float_DoubleUnderscore")]
    [InlineData("Number_Float_UnderscoreBeforeDot")]
    [InlineData("Number_Float_TrailingUnderscore")]
    [InlineData("Number_Float_InvalidChar")]
    [InlineData("Number_Float_InvalidTrailingChar")]
    [InlineData("Number_Float_EmptyAfterE")]
    [InlineData("Number_Float_UnderscoreBeforeE")]
    [InlineData("Number_Float_UnderscoreAfterE")]
    [InlineData("Number_Float_UnderscoreBeforePlus")]
    [InlineData("Number_Float_UnderscoreAfterPlus")]
    [InlineData("Number_Float_UnderscoreBeforeMinus")]
    [InlineData("Number_Float_UnderscoreAfterMinus")]
    [InlineData("Number_Imaginary_UnderscoreBeforeJ")]
    [InlineData("Number_Imaginary_UnderscoreAfterJ")]
    [InlineData("Number_Imaginary_InvalidCharBeforeJ")]
    [InlineData("Number_Imaginary_InvalidCharAfterJ")]
    [InlineData("String_PrefixUR")]
    [InlineData("String_PrefixUB")]
    [InlineData("String_UnclosedOnLine_SingleQuote")]
    [InlineData("String_UnclosedOnFile_SingleQuote")]
    [InlineData("String_UnclosedOnFile_TripleQuote_SingleLine")]
    [InlineData("String_UnclosedOnFile_TripleQuote_MultiLine")]
    [InlineData("String_UnclosedOnFile_EscapedQuote")]
    [InlineData("Partial_SingleQuoteUnterminatedEOF")]
    [InlineData("Partial_SingleQuoteUnterminatedNewline")]
    [InlineData("Partial_TripleQuoteUnterminatedEOF")]
    [InlineData("Partial_InterpolationUnclosedBraceEOF", TokenizerError.UnclosedInterpolationExpression)]
    [InlineData("Partial_InterpolationLineLimitExceeded_Fatal", TokenizerError.TooLongInterpolationExpression)]
    [InlineData("Partial_NestedUnterminatedString(Only one error expecting)")]
    [InlineData("Partial_NestingDepthExceeded_Fatal", TokenizerError.InterpolatedStringNestingOverflow)]
    [InlineData("Partial_FormatSpecNestedInterpolationLineLimit_Fatal", TokenizerError.TooLongInterpolationExpression)]
    [InlineData("Partial_InvalidPrefix_bf")]
    [InlineData("Partial_InvalidPrefix_fb")]
    [InlineData("Partial_InvalidPrefix_bt")]
    [InlineData("Partial_InvalidPrefix_tb")]
    [InlineData("Partial_InvalidPrefix_ft")]
    [InlineData("Partial_InvalidPrefix_tf")]
    [InlineData("Partial_InvalidShieldedBrace")]
    public void TestLiteralErrors(string @case, TokenizerError error = TokenizerError.InvalidLiteral)
    {
        Debug.Assert(literal_errors_test_cases.ContainsKey(@case));

        (string code, string message, var expected) = literal_errors_test_cases[@case];

        var tokenizer = test(code, expected);

        Assert.Equal(error, tokenizer.Error);
        Assert.Equal(message, tokenizer.ErrorMessage);
    }

    private const string tabs = "Tabs and spaces mixing is not allowed.";

    private static readonly Dictionary<string, (string code, string message, List<Token> expected)> indentation_errors_test_cases =
        new()
        {
            ["IndentationMismatch"] = ("""
            bau
                    bau
                bau
            """,
            "Can dedent only on existing indentation level.",
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Error, empty), new(Name, "bau"),
                // Error token replaces Dedent, so we releasing it is not needed.
                eof,
            ]),
            ["AlternateIndentOnSameLevel"] = ("bau\n        bau\n\tbau", tabs,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Error, empty), new(Name, "bau"),
                new(Dedent, empty), // Release indentation at the EOF.
                eof,
            ]),
            ["AlternateIndentOnReducingLevel"] = ("bau\n        bau\n            bau\n\tbau", tabs,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "        "), new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "            "), new(Name, "bau"), new(NewLine, "\n"),
                new(Error, empty), new(Name, "bau"),
                new(Dedent, empty), // Release indentation at the EOF.
                eof,
            ]),
            ["AlternateIndentOnIncreaseLevel"] = ("bau\n    bau\n\tbau", tabs,
            [
                new(Name, "bau"), new(NewLine, "\n"),
                new(Indent, "    "), new(Name, "bau"), new(NewLine, "\n"),
                new(Error, empty), new(Name, "bau"),
                new(Dedent, empty), // Release indentation at the EOF.
                eof
            ]),
        };

    [Theory]
    [InlineData("IndentationMismatch")]
    [InlineData("AlternateIndentOnSameLevel")]
    [InlineData("AlternateIndentOnReducingLevel")]
    [InlineData("AlternateIndentOnIncreaseLevel")]
    public void TestIndentationErrors(string @case)
    {
        Debug.Assert(indentation_errors_test_cases.ContainsKey(@case));
        (string code, string message, var expected) = indentation_errors_test_cases[@case];

        var tok = test(code, expected);

        Assert.Equal(TokenizerError.IndentationError, tok.Error);
        Assert.Equal(message, tok.ErrorMessage);
    }

    private static readonly Dictionary<string, (string code, List<Token> expected)> pstring_test_cases =
        new()
        {
            // This test cases were taken from CPython (https://github.com/python/cpython/blob/main/Lib/test/test_tokenize.py#L450-L610)
            ["F_Empty"] = ("""
            f"bau"
            """,
            [
                new(FStringStart, "f\""),
                new(FStringMiddle, "bau"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_BasicAndRawPrefix"] = ("""
            fR"b{a}u"
            """,
            [
                new(FStringStart, "fR\""),
                new(FStringMiddle, "b"),
                new(LeftBrace, "{"),
                new(Name, "a"),
                new(RightBrace, "}"),
                new(FStringMiddle, "u"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_ConversionSpecAndShieldedBracesAndRawPrefix"] = ("""
            fR"b{{{a!r}}}u"
            """,
            [
                new(FStringStart, "fR\""),
                new(FStringMiddle, "b{"),
                new(LeftBrace, "{"),
                new(Name, "a"),
                new(Exclamation, "!"),
                new(Name, "r"),
                new(RightBrace, "}"),
                new(FStringMiddle, "}"),
                new(FStringMiddle, "u"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_ExpressionInsideAndShieldedBraces"] = ("""
            f"{{{1+1}}}"
            """,
            [
                new(FStringStart, "f\""),
                new(FStringMiddle, "{"),
                new(LeftBrace, "{"),
                new(Number, "1"),
                new(Plus, "+"),
                new(Number, "1"),
                new(RightBrace, "}"),
                new(FStringMiddle, "}"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_NestedFStrings"] = ("f\"\"\"{f'''{f'{f\"{1+1}\"}'}'''}\"\"\"",
            [
                new(FStringStart, "f\"\"\""),
                new(LeftBrace,    "{"),
                new(FStringStart, "f'''"),
                new(LeftBrace,    "{"),
                new(FStringStart, "f'"),
                new(LeftBrace,    "{"),
                new(FStringStart, "f\""),
                new(LeftBrace,    "{"),
                new(Number,       "1"),
                new(Plus,         "+"),
                new(Number,       "1"),
                new(RightBrace,   "}"),
                new(FStringEnd,   "\""),
                new(RightBrace,   "}"),
                new(FStringEnd,   "'"),
                new(RightBrace,   "}"),
                new(FStringEnd,   "'''"),
                new(RightBrace,   "}"),
                new(FStringEnd,   "\"\"\""),
                eof,
            ]),
            ["F_TripleQuotedWithLineFeedAndConversionSpec"] =
            ("f\"\"\"     x\nbau(data, encoding={invalid!r})\n\"\"\"",
            [
                new(FStringStart,  "f\"\"\""),
                new(FStringMiddle, "     x\nbau(data, encoding="),
                new(LeftBrace,     "{"),
                new(Name,          "invalid"),
                new(Exclamation,   "!"),
                new(Name,          "r"),
                new(RightBrace,    "}"),
                new(FStringMiddle, ")\n"),
                new(FStringEnd,    "\"\"\""),
                eof,
            ]),
            ["F_TripleQuotedBasicWithLineFeed"] =
            ("f\"\"\"123456789\nsomething{None}bau\"\"\"",
            [
                new(FStringStart, "f\"\"\""),
                new(FStringMiddle, "123456789\nsomething"),
                new(LeftBrace, "{"),
                new(Name, "None"),
                new(RightBrace, "}"),
                new(FStringMiddle, "bau"),
                new(FStringEnd, "\"\"\""),
                eof,
            ]),
            ["F_TripleQuotedEmpty"] = ("f\"\"\"bau\"\"\"",
            [
                new(FStringStart, "f\"\"\""),
                new(FStringMiddle, "bau"),
                new(FStringEnd, "\"\"\""),
                eof,
            ]),
            ["F_StringWithJoiningLineContinuation"] = ("f\"bau\\\ndef\"",
            [
                new(FStringStart, "f\""),
                new(FStringMiddle, "bau\\\ndef"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_StringWithJoiningLineContinuationAndRaw"] =
            ("Rf\"bau\\\ndef\"",
            [
                new(FStringStart, "Rf\""),
                new(FStringMiddle, "bau\\\ndef"),
                new(FStringEnd, "\""),
                eof,
            ]),
            ["F_MultiplePlaceholdersAndFormatSpecAndDebugSpec"] =
            ("f'baubaubau1 {f+w:.3f} baubaubau2 {m+c=} bababababau'",
            [
                new(FStringStart, "f'"),
                new(FStringMiddle, "baubaubau1 "),
                new(LeftBrace, "{"),
                new(Name, "f"),
                new(Plus, "+"),
                new(Name, "w"),
                new(Colon, ":"),
                new(FStringMiddle, ".3f"),
                new(RightBrace, "}"),
                new(FStringMiddle, " baubaubau2 "),
                new(LeftBrace, "{"),
                new(Name, "m"),
                new(Plus, "+"),
                new(Name, "c"),
                new(Equal, "="),
                new(RightBrace, "}"),
                new(FStringMiddle, " bababababau"),
                new(FStringEnd, "'"),
                eof,
            ]),
            ["F_TripleQuotedWithLineFeedInPlaceholderAndDebugSpec"] = ("""
            f'''{
            3
            =}'''
            """,
            [
                new(FStringStart, "f'''"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(Number, "3"),
                new(TriviaNewLine, "\n"),
                new(Equal, "="),
                new(RightBrace, "}"),
                new(FStringEnd, "'''"),
                eof,
            ]),
            ["F_TripleQuotedWithLineFeedInPlaceHolderAndFormatSpec"] = ("""
            f'''__{
                f:m
            }__'''
            """,
            [
                new(FStringStart, "f'''"),
                new(FStringMiddle, "__"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "),
                new(Name, "f"),
                new(Colon, ":"),
                new(FStringMiddle, "m\n"),
                new(RightBrace, "}"),
                new(FStringMiddle, "__"),
                new(FStringEnd, "'''"),
                eof,
            ]),
            ["F_TripleQuotedWithWeirdFormatSpec(ShouldToWorksAnyway)"] = ("""
            f'''__{
                b:f
                w
                 m
                  c
            }__'''
            """,
            [
                new(FStringStart, "f'''"),
                new(FStringMiddle, "__"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "),
                new(Name, "b"),
                new(Colon, ":"),
                new(FStringMiddle, "f\n    w\n     m\n      c\n"),
                new(RightBrace, "}"),
                new(FStringMiddle, "__"),
                new(FStringEnd, "'''"),
                eof,
            ]),
            // Maybe remove DebugSpecString? Maybe, maybe...
            ["F_VariousDebugStrings"] = ("""
            f"{bau=}"
            f"{bau =}"
            f"{bau= }"
            f"{bau = }"
            """,
            [
                new(FStringStart,         "f\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(Equal,                "="),
                new(RightBrace,           "}"),
                new(FStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(FStringStart,         "f\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(WhiteSpace,           " "),
                new(Equal,                "="),
                new(RightBrace,           "}"),
                new(FStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(FStringStart,         "f\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(Equal,                "="),
                new(WhiteSpace,           " "),
                new(RightBrace,           "}"),
                new(FStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(FStringStart,         "f\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(WhiteSpace,           " "),
                new(Equal,                "="),
                new(WhiteSpace,           " "),
                new(RightBrace,           "}"),
                new(FStringEnd,           "\""),
                eof,
            ]),
            ["F_InterpolatedFormatSpec"] =
            ("f'{bau:.0{fwmc}f}'",
            [
                new(FStringStart, "f'"),
                new(LeftBrace, "{"),
                new(Name, "bau"),
                new(Colon, ":"),
                new(FStringMiddle, ".0"),
                new(LeftBrace, "{"),
                new(Name, "fwmc"),
                new(RightBrace, "}"),
                new(FStringMiddle, "f"),
                new(RightBrace, "}"),
                new(FStringEnd, "'"),
                eof,
            ]),
            // Copy-paste previous for t-strings.
            ["T_Empty"] = ("""
            t"bau"
            """,
            [
                new(TStringStart, "t\""),
                new(TStringMiddle, "bau"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_BasicAndRawPrefix"] = ("""
            tR"b{a}u"
            """,
            [
                new(TStringStart, "tR\""),
                new(TStringMiddle, "b"),
                new(LeftBrace, "{"),
                new(Name, "a"),
                new(RightBrace, "}"),
                new(TStringMiddle, "u"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_ConversionSpecAndShieldedBracesAndRawPrefix"] = ("""
            tR"b{{{a!r}}}u"
            """,
            [
                new(TStringStart, "tR\""),
                new(TStringMiddle, "b{"),
                new(LeftBrace, "{"),
                new(Name, "a"),
                new(Exclamation, "!"),
                new(Name, "r"),
                new(RightBrace, "}"),
                new(TStringMiddle, "}"),
                new(TStringMiddle, "u"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_ExpressionInsideAndShieldedBraces"] = ("""
            t"{{{1+1}}}"
            """,
            [
                new(TStringStart, "t\""),
                new(TStringMiddle, "{"),
                new(LeftBrace, "{"),
                new(Number, "1"),
                new(Plus, "+"),
                new(Number, "1"),
                new(RightBrace, "}"),
                new(TStringMiddle, "}"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_NestedTStrings"] = ("t\"\"\"{t'''{t'{t\"{1+1}\"}'}'''}\"\"\"",
            [
                new(TStringStart, "t\"\"\""),
                new(LeftBrace,    "{"),
                new(TStringStart, "t'''"),
                new(LeftBrace,    "{"),
                new(TStringStart, "t'"),
                new(LeftBrace,    "{"),
                new(TStringStart, "t\""),
                new(LeftBrace,    "{"),
                new(Number,       "1"),
                new(Plus,         "+"),
                new(Number,       "1"),
                new(RightBrace,   "}"),
                new(TStringEnd,   "\""),
                new(RightBrace,   "}"),
                new(TStringEnd,   "'"),
                new(RightBrace,   "}"),
                new(TStringEnd,   "'''"),
                new(RightBrace,   "}"),
                new(TStringEnd,   "\"\"\""),
                eof,
            ]),
            ["T_TripleQuotedWithLineFeedAndConversionSpec"] =
            ("t\"\"\"     x\nbau(data, encoding={invalid!r})\n\"\"\"",
            [
                new(TStringStart,  "t\"\"\""),
                new(TStringMiddle, "     x\nbau(data, encoding="),
                new(LeftBrace,     "{"),
                new(Name,          "invalid"),
                new(Exclamation,   "!"),
                new(Name,          "r"),
                new(RightBrace,    "}"),
                new(TStringMiddle, ")\n"),
                new(TStringEnd,    "\"\"\""),
                eof,
            ]),
            ["T_TripleQuotedBasicWithLineFeed"] =
            ("t\"\"\"123456789\nsomething{None}bau\"\"\"",
            [
                new(TStringStart, "t\"\"\""),
                new(TStringMiddle, "123456789\nsomething"),
                new(LeftBrace, "{"),
                new(Name, "None"),
                new(RightBrace, "}"),
                new(TStringMiddle, "bau"),
                new(TStringEnd, "\"\"\""),
                eof,
            ]),
            ["T_TripleQuotedEmpty"] = ("t\"\"\"bau\"\"\"",
            [
                new(TStringStart, "t\"\"\""),
                new(TStringMiddle, "bau"),
                new(TStringEnd, "\"\"\""),
                eof,
            ]),
            ["T_StringWithJoiningLineContinuation"] = ("t\"bau\\\ndef\"",
            [
                new(TStringStart, "t\""),
                new(TStringMiddle, "bau\\\ndef"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_StringWithJoiningLineContinuationAndRaw"] =
            ("Rt\"bau\\\ndef\"",
            [
                new(TStringStart, "Rt\""),
                new(TStringMiddle, "bau\\\ndef"),
                new(TStringEnd, "\""),
                eof,
            ]),
            ["T_MultiplePlaceholdersAndFormatSpecAndDebugSpec"] =
            ("t'baubaubau1 {f+w:.3f} baubaubau2 {m+c=} bababababau'",
            [
                new(TStringStart, "t'"),
                new(TStringMiddle, "baubaubau1 "),
                new(LeftBrace, "{"),
                new(Name, "f"),
                new(Plus, "+"),
                new(Name, "w"),
                new(Colon, ":"),
                new(TStringMiddle, ".3f"),
                new(RightBrace, "}"),
                new(TStringMiddle, " baubaubau2 "),
                new(LeftBrace, "{"),
                new(Name, "m"),
                new(Plus, "+"),
                new(Name, "c"),
                new(Equal, "="),
                new(RightBrace, "}"),
                new(TStringMiddle, " bababababau"),
                new(TStringEnd, "'"),
                eof,
            ]),
            ["T_TripleQuotedWithLineFeedInPlaceholderAndDebugSpec"] = ("""
            t'''{
            3
            =}'''
            """,
            [
                new(TStringStart, "t'''"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(Number, "3"),
                new(TriviaNewLine, "\n"),
                new(Equal, "="),
                new(RightBrace, "}"),
                new(TStringEnd, "'''"),
                eof,
            ]),
            ["T_TripleQuotedWithLineFeedInPlaceHolderAndFormatSpec"] = ("""
            t'''__{
                f:m
            }__'''
            """,
            [
                new(TStringStart, "t'''"),
                new(TStringMiddle, "__"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "),
                new(Name, "f"),
                new(Colon, ":"),
                new(TStringMiddle, "m\n"),
                new(RightBrace, "}"),
                new(TStringMiddle, "__"),
                new(TStringEnd, "'''"),
                eof,
            ]),
            ["T_TripleQuotedWithWeirdFormatSpec(ShouldToWorksAnyway)"] = ("""
            t'''__{
                b:f
                w
                 m
                  c
            }__'''
            """,
            [
                new(TStringStart, "t'''"),
                new(TStringMiddle, "__"),
                new(LeftBrace, "{"),
                new(TriviaNewLine, "\n"),
                new(WhiteSpace, "    "),
                new(Name, "b"),
                new(Colon, ":"),
                new(TStringMiddle, "f\n    w\n     m\n      c\n"),
                new(RightBrace, "}"),
                new(TStringMiddle, "__"),
                new(TStringEnd, "'''"),
                eof,
            ]),
            ["T_VariousDebugStrings"] = ("""
            t"{bau=}"
            t"{bau =}"
            t"{bau= }"
            t"{bau = }"
            """,
            [
                new(TStringStart,         "t\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(Equal,                "="),
                new(RightBrace,           "}"),
                new(TStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(TStringStart,         "t\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(WhiteSpace,           " "),
                new(Equal,                "="),
                new(RightBrace,           "}"),
                new(TStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(TStringStart,         "t\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(Equal,                "="),
                new(WhiteSpace,           " "),
                new(RightBrace,           "}"),
                new(TStringEnd,           "\""),
                new(NewLine,              "\n"),

                new(TStringStart,         "t\""),
                new(LeftBrace,            "{"),
                new(Name,                 "bau"),
                new(WhiteSpace,           " "),
                new(Equal,                "="),
                new(WhiteSpace,           " "),
                new(RightBrace,           "}"),
                new(TStringEnd,           "\""),
                eof,
            ]),
            ["T_InterpolatedFormatSpec"] =
            ("t'{bau:.0{fwmc}f}'",
            [
                new(TStringStart, "t'"),
                new(LeftBrace, "{"),
                new(Name, "bau"),
                new(Colon, ":"),
                new(TStringMiddle, ".0"),
                new(LeftBrace, "{"),
                new(Name, "fwmc"),
                new(RightBrace, "}"),
                new(TStringMiddle, "f"),
                new(RightBrace, "}"),
                new(TStringEnd, "'"),
                eof,
            ]),
            // Mixed scenario.
            ["Mixed"] = ("""
            t"BAU {f"bau={fwmc}"} IN BAUBAU"
            """,
            [
                new(TStringStart,  "t\""),
                new(TStringMiddle, "BAU "),
                new(LeftBrace,     "{"),
                new(FStringStart,  "f\""),
                new(FStringMiddle, "bau="),
                new(LeftBrace,     "{"),
                new(Name,          "fwmc"),
                new(RightBrace,    "}"),
                new(FStringEnd,    "\""),
                new(RightBrace,    "}"),
                new(TStringMiddle, " IN BAUBAU"),
                new(TStringEnd,    "\""),
                eof,
            ]),
            ["Common_ShieldedBraces"] = ("""
            f"bau {{ }}"
            """,
            [
                new(FStringStart, "f\""),
                new(FStringMiddle, "bau {"),
                new(FStringMiddle, " }"),
                new(FStringEnd, "\""),
                eof,
            ]),
        };

    [Theory()]
    [InlineData("F_Empty")]
    [InlineData("F_BasicAndRawPrefix")]
    [InlineData("F_ConversionSpecAndShieldedBracesAndRawPrefix")]
    [InlineData("F_ExpressionInsideAndShieldedBraces")]
    [InlineData("F_NestedFStrings")]
    [InlineData("F_TripleQuotedWithLineFeedAndConversionSpec")]
    [InlineData("F_TripleQuotedBasicWithLineFeed")]
    [InlineData("F_TripleQuotedEmpty")]
    [InlineData("F_StringWithJoiningLineContinuation")]
    [InlineData("F_StringWithJoiningLineContinuationAndRaw")]
    [InlineData("F_MultiplePlaceholdersAndFormatSpecAndDebugSpec")]
    [InlineData("F_TripleQuotedWithLineFeedInPlaceholderAndDebugSpec")]
    [InlineData("F_TripleQuotedWithLineFeedInPlaceHolderAndFormatSpec")]
    [InlineData("F_TripleQuotedWithWeirdFormatSpec(ShouldToWorksAnyway)")]
    [InlineData("F_VariousDebugStrings")]
    [InlineData("F_InterpolatedFormatSpec")]
    [InlineData("T_Empty")]
    [InlineData("T_BasicAndRawPrefix")]
    [InlineData("T_ConversionSpecAndShieldedBracesAndRawPrefix")]
    [InlineData("T_ExpressionInsideAndShieldedBraces")]
    [InlineData("T_NestedTStrings")]
    [InlineData("T_TripleQuotedWithLineFeedAndConversionSpec")]
    [InlineData("T_TripleQuotedBasicWithLineFeed")]
    [InlineData("T_TripleQuotedEmpty")]
    [InlineData("T_StringWithJoiningLineContinuation")]
    [InlineData("T_StringWithJoiningLineContinuationAndRaw")]
    [InlineData("T_MultiplePlaceholdersAndFormatSpecAndDebugSpec")]
    [InlineData("T_TripleQuotedWithLineFeedInPlaceholderAndDebugSpec")]
    [InlineData("T_TripleQuotedWithLineFeedInPlaceHolderAndFormatSpec")]
    [InlineData("T_TripleQuotedWithWeirdFormatSpec(ShouldToWorksAnyway)")]
    [InlineData("T_VariousDebugStrings")]
    [InlineData("T_InterpolatedFormatSpec")]
    [InlineData("Mixed")]
    [InlineData("Common_ShieldedBraces")]
    public void TestPartialStrings(string @case)
    {
        Debug.Assert(pstring_test_cases.ContainsKey(@case));

        (string code, var expected) = pstring_test_cases[@case];
        test(code, expected);
    }

    private static Tokenizer test(string code, IList<Token> expected)
    {
        var sync = SynchronizationPoint.ClearPoint(new StringBuffer(code));

        var tokenizer = new Tokenizer(sync);

        List<Token> result = [];
        do
        {
            tokenizer.ReadNext(out var token);
            result.Add(token.Value);
        }
        while (!tokenizer.ShouldStop);

        Assert.True(tokenizer.ShouldStop);

        Assert.Equal(expected.Count, result.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            var exp = expected[i];
            var res = result[i];
            Assert.Equal(exp.Lexeme, res.Lexeme);
        }

        return tokenizer;
    }

    private static readonly ReadOnlyMemory<char> empty = ReadOnlyMemory<char>.Empty;

    private static Token eof => new(EndOfFile, empty);
}
