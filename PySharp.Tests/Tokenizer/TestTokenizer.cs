using System.Diagnostics;
using PySharp.Tokenizer;
using static PySharp.Tokenizer.TokenType;

namespace PySharp.Tests.Tokenizer;

public class TestTokenizer
{
    private static readonly Dictionary<string, (string code, IList<Token> expected)> one_row_test_cases =
        new()
        {
            ["String_SimpleSingle"] = ("'123'", [new(StringLiteral, "'123'", (0, 0), (0, 5)), eof(0, 5)]),
            ["String_SimpleDouble"] = ("\"123\"", [new(StringLiteral, "\"123\"", (0, 0), (0, 5)), eof(0, 5)]),
            ["String_SpaceBoth"] = ("'123' \"123\"", [new(StringLiteral, "'123'", (0, 0), (0, 5)),
                                                      new(StringLiteral, "\"123\"", (0, 6), (0, 11)), eof(0, 11)]),
            ["String_Empty"] = ("''", [new(StringLiteral, "''", (0, 0), (0, 2)), eof(0, 2)]),
            ["String_raw"] = ("r'123'", [new(StringLiteral, "r'123'", (0, 0), (0, 6)), eof(0, 6)]),
            ["String_RAW"] = ("R'123'", [new(StringLiteral, "R'123'", (0, 0), (0, 6)), eof(0, 6)]),
            ["String_byte"] = ("b'123'", [new(StringLiteral, "b'123'", (0, 0), (0, 6)), eof(0, 6)]),
            ["String_BYTE"] = ("B'123'", [new(StringLiteral, "B'123'", (0, 0), (0, 6)), eof(0, 6)]),
            ["String_RawByte"] = ("rb'123'", [new(StringLiteral, "rb'123'", (0, 0), (0, 7)), eof(0, 7)]),
            ["String_ByteRaw"] = ("br'123'", [new(StringLiteral, "br'123'", (0, 0), (0, 7)), eof(0, 7)]),
            ["String_EscapedQuote"] = (@"'bau\'bau'", [new(StringLiteral, @"'bau\'bau'", (0, 0), (0, 10)), eof(0, 10)]),

            ["Name_Simple"] = ("bau", [new(Name, "bau", (0, 0), (0, 3)), eof(0, 3)]),
            ["Name_Space"] = ("bau bau", [new(Name, "bau", (0, 0), (0, 3)), new(Name, "bau", (0, 4), (0, 7)), eof(0, 7)]),
            ["Name_TrailingUnderscore"] = ("baubau__", [new(Name, "baubau__", (0, 0), (0, 8)), eof(0, 8)]),
            ["Name_LeadingUnderscore"] = ("__bau", [new(Name, "__bau", (0, 0), (0, 5)), eof(0, 5)]),
            ["Name_BetweenUnderscore"] = ("__bau__", [new(Name, "__bau__", (0, 0), (0, 7)), eof(0, 7)]),
            ["Name_WithDigits"] = ("b123", [new(Name, "b123", (0, 0), (0, 4)), eof(0, 4)]),
            ["Name_Complex"] = ("bau_123_bau__", [new(Name, "bau_123_bau__", (0, 0), (0, 13)), eof(0, 13)]),

            ["Number_Simple"] = ("123", [new(Number, "123", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_Space"] = ("10 10", [new(Number, "10", (0, 0), (0, 2)), new(Number, "10", (0, 3), (0, 5)), eof(0, 5)]),
            ["Number_Zeros"] = ("000", [new(Number, "000", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_FractionZeros"] = ("0.0", [new(Number, "0.0", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_FractionLeadingZeros"] = ("01.10", [new(Number, "01.10", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_FractionLeadingZerosDot"] = ("01.", [new(Number, "01.", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_LeadingDot"] = (".10", [new(Number, ".10", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_TrailingDot"] = ("10.", [new(Number, "10.", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_SimpleExponent"] = ("10e2", [new(Number, "10e2", (0, 0), (0, 4)), eof(0, 4)]),
            ["Number_PlusExponent"] = ("10e+2", [new(Number, "10e+2", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_MinusExponent"] = ("10e-2", [new(Number, "10e-2", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_DotExponent"] = ("10.e2", [new(Number, "10.e2", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_SimpleImaginary"] = ("10j", [new(Number, "10j", (0, 0), (0, 3)), eof(0, 3)]),
            ["Number_ImaginaryAfterDot"] = ("10.j", [new(Number, "10.j", (0, 0), (0, 4)), eof(0, 4)]),
            ["Number_ImaginaryAfterExpo"] = ("10e1j", [new(Number, "10e1j", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_ImaginaryAfterDotExpo"] = ("10.e1j", [new(Number, "10.e1j", (0, 0), (0, 6)), eof(0, 6)]),
            ["Number_NormalUnderscores"] = ("1_000_000", [new(Number, "1_000_000", (0, 0), (0, 9)), eof(0, 9)]),
            ["Number_WildUnderscores"] = ("10_0_0_0_0", [new(Number, "10_0_0_0_0", (0, 0), (0, 10)), eof(0, 10)]),
            ["Number_FractionUnderscores"] = ("1_1.1_1", [new(Number, "1_1.1_1", (0, 0), (0, 7)), eof(0, 7)]),
            ["Number_Complex1"] = ("1_2_3.1_2_3e+2j", [new(Number, "1_2_3.1_2_3e+2j", (0, 0), (0, 15)), eof(0, 15)]),
            ["Number_Complex2"] = (".1_2_3e-2j", [new(Number, ".1_2_3e-2j", (0, 0), (0, 10)), eof(0, 10)]),
            ["Number_Complex3"] = ("1_2_3.1_2_3e+2_0j", [new(Number, "1_2_3.1_2_3e+2_0j", (0, 0), (0, 17)), eof(0, 17)]),

            ["Number_Hexadecimal"] = ("0x123", [new(Number, "0x123", (0, 0), (0, 5)), eof(0, 5)]),
            ["Number_HexadecimalFullLower"] = ("0x0123456789abcdef", [new(Number, "0x0123456789abcdef", (0, 0), (0, 18)), eof(0, 18)]),
            ["Number_HexadecimalFullUpper"] = ("0x0123456789ABCDEF", [new(Number, "0x0123456789ABCDEF", (0, 0), (0, 18)), eof(0, 18)]),
            ["Number_HexadecimalMixed"] = ("0xAbCdEf", [new(Number, "0xAbCdEf", (0, 0), (0, 8)), eof(0, 8)]),
            ["Number_HexadecimalUnderscores"] = ("0x33_22_11", [new(Number, "0x33_22_11", (0, 0), (0, 10)), eof(0, 10)]),
            ["Number_HexadecimalLeadUnderscores"] = ("0x_33_22_11", [new(Number, "0x_33_22_11", (0, 0), (0, 11)), eof(0, 11)]),
            ["Number_OctalFull"] = ("0o01234567", [new(Number, "0o01234567", (0, 0), (0, 10)), eof(0, 10)]),
            ["Number_OctalUnderscores"] = ("0o_123_456_7", [new(Number, "0o_123_456_7", (0, 0), (0, 12)), eof(0, 12)]),
            ["Number_BinaryFull"] = ("0b01", [new(Number, "0b01", (0, 0), (0, 4)), eof(0, 4)]),
            ["Number_BinaryUnderscores"] = ("0b_1_0", [new(Number, "0b_1_0", (0, 0), (0, 6)), eof(0, 6)]),
            ["Number_BinaryLong"] = ("0b101010100100100010101001", [new(Number, "0b101010100100100010101001", (0, 0), (0, 26)), eof(0, 26)]),

            // Please don't touch it. I spent a lot on these indexes...
            ["Op_AllExceptParensAndDots"] = (
                ", : := ; = == + += - -= -> * *= ** **= / /= // //= % %= & &= | |= @ @= ^ ^= ~ > >= >> >>= < <= << <<= !",
                [
                    new(Comma,            ",",   (0,   0),  (0,   1)),
                    new(Colon,            ":",   (0,   2),  (0,   3)),
                    new(ColonEqual,       ":=",  (0,   4),  (0,   6)),
                    new(Semicolon,        ";",   (0,   7),  (0,   8)),
                    new(Equal,            "=",   (0,   9),  (0,  10)),
                    new(EqEqual,          "==",  (0,  11),  (0,  13)),
                    new(Plus,             "+",   (0,  14),  (0,  15)),
                    new(PlusEqual,        "+=",  (0,  16),  (0,  18)),
                    new(Minus,            "-",   (0,  19),  (0,  20)),
                    new(MinusEqual,       "-=",  (0,  21),  (0,  23)),
                    new(RightArrow,       "->",  (0,  24),  (0,  26)),
                    new(Star,             "*",   (0,  27),  (0,  28)),
                    new(StarEqual,        "*=",  (0,  29),  (0,  31)),
                    new(DoubleStar,       "**",  (0,  32),  (0,  34)),
                    new(DoubleStarEqual,  "**=", (0,  35),  (0,  38)),
                    new(Slash,            "/",   (0,  39),  (0,  40)),
                    new(SlashEqual,       "/=",  (0,  41),  (0,  43)),
                    new(DoubleSlash,      "//",  (0,  44),  (0,  46)),
                    new(DoubleSlashEqual, "//=", (0,  47),  (0,  50)),
                    new(Percent,          "%",   (0,  51),  (0,  52)),
                    new(PercentEqual,     "%=",  (0,  53),  (0,  55)),
                    new(Ampersand,        "&",   (0,  56),  (0,  57)),
                    new(AmpersandEqual,   "&=",  (0,  58),  (0,  60)),
                    new(VertBar,          "|",   (0,  61),  (0,  62)),
                    new(VertBarEqual,     "|=",  (0,  63),  (0,  65)),
                    new(At,               "@",   (0,  66),  (0,  67)),
                    new(AtEqual,          "@=",  (0,  68),  (0,  70)),
                    new(Circumflex,       "^",   (0,  71),  (0,  72)),
                    new(CircumflexEqual,  "^=",  (0,  73),  (0,  75)),
                    new(Tilde,            "~",   (0,  76),  (0,  77)),
                    new(Greater,          ">",   (0,  78),  (0,  79)),
                    new(GreaterEqual,     ">=",  (0,  80),  (0,  82)),
                    new(RightShift,       ">>",  (0,  83),  (0,  85)),
                    new(RightShiftEqual,  ">>=", (0,  86),  (0,  89)),
                    new(Less,             "<",   (0,  90),  (0,  91)),
                    new(LessEqual,        "<=",  (0,  92),  (0,  94)),
                    new(LeftShift,        "<<",  (0,  95),  (0,  97)),
                    new(LeftShiftEqual,   "<<=", (0,  98),  (0, 101)),
                    new(Exclamation,      "!",   (0, 102),  (0, 103)),
                    eof(0, 103),
                ]
            ),
            // Since dots related to numbers they're separated.
            ["Op_Dot"] = (".", [new(Dot, ".", (0, 0), (0, 1)), eof(0, 1)]),
            ["Op_DotSpace"] = (". .", [new(Dot, ".", (0, 0), (0, 1)), new(Dot, ".", (0, 2), (0, 3)), eof(0, 3)]),
            ["Op_Ellipsis"] = ("...", [new(Ellipsis, "...", (0, 0), (0, 3)), eof(0, 3)]),
            ["Op_EllipsisSpace"] = ("... ...", [new(Ellipsis, "...", (0, 0), (0, 3)), new(Ellipsis, "...", (0, 4), (0, 7)), eof(0, 7)]),
            ["Op_EllipsisDot"] = ("... .", [new(Ellipsis, "...", (0, 0), (0, 3)), new(Dot, ".", (0, 4), (0, 5)), eof(0, 5)]),
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
    [InlineData("Op_AllExceptParensAndDots")]
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
            """, [new(StringLiteral, "'''bau'''", (0, 0), (0, 9)), eof(0, 9)]),
            ["MixedQuotes"] = ("''' \"\"\" '''", [new(StringLiteral, "''' \"\"\" '''", (0, 0), (0, 11)), eof(0, 11)]),
            ["EscapedQuote"] = ("""
            ''' \' '''
            """,
            [new(StringLiteral, "''' \\' '''", (0, 0), (0, 10)), eof(0, 10)]),
            ["OneLineFeed"] = ("""
            '''bau
            bau'''
            """, [new(StringLiteral, "'''bau\nbau'''", (0, 0), (1, 6)), eof(1, 6)]),
            ["EscapedEndOfLine"] = ("""
            '''bau\
            bau'''
            """, [new(StringLiteral, "'''bau\\\nbau'''", (0, 0), (1, 6)), eof(1, 6)]),
            ["LineFeedCRLF"] = ("'''bau\r\nbau'''", [new(StringLiteral, "'''bau\r\nbau'''", (0, 0), (1, 6)), eof(1, 6)]),
            ["LineFeedCR"] = ("'''bau\rbau'''", [new(StringLiteral, "'''bau\rbau'''", (0, 0), (1, 6)), eof(1, 6)]),
            ["Empty"] = ("''''''", [new(StringLiteral, "''''''", (0, 0), (0, 6)), eof(0, 6)]),
            ["Raw"] = (@"r'''b\au'''", [new(StringLiteral, @"r'''b\au'''", (0, 0), (0, 11)), eof(0, 11)]),
            ["Byte"] = (@"b'''r\au'''", [new(StringLiteral, @"b'''r\au'''", (0, 0), (0, 11)), eof(0, 11)]),
            ["ByteRaw"] = (@"rb'''me\ow'''", [new(StringLiteral, @"rb'''me\ow'''", (0, 0), (0, 13)), eof(0, 13)]),
            ["LongString"] = ("""
            '''bau1"
            bau2'
            bau3
            bau4'
            '''
            """,
            [new(StringLiteral, "'''bau1\"\nbau2'\nbau3\nbau4'\n'''", (0, 0), (4, 3)), eof(4, 3)]),
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

    private static readonly Dictionary<string, (string code, bool trivia, IList<Token> expected)> parens_test_cases =
        new()
        {
            ["AllInRow"] = ("({[]})", false, [
                new(LeftParen, "(", (0, 0), (0, 1)),
                new(LeftBrace, "{", (0, 1), (0, 2)),
                new(LeftSquareBracket, "[", (0, 2), (0, 3)),
                new(RightSquareBracket, "]", (0, 3), (0, 4)),
                new(RightBrace, "}", (0, 4), (0, 5)),
                new(RightParen, ")", (0, 5), (0, 6)),
                eof(0, 6),
            ]),
            ["TriviaNewLine_Paren"] = ("(\n)", true, [
                new(LeftParen, "(", (0, 0), (0, 1)),
                new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(RightParen, ")", (1, 0), (1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_Brace"] = ("{\n}", true, [
                new(LeftBrace, "{", (0, 0), (0, 1)),
                new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(RightBrace, "}", (1, 0), (1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_SqBrt"] = ("[\n]", true, [
                new(LeftSquareBracket, "[", (0, 0), (0, 1)),
                new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(RightSquareBracket, "]", (1, 0), (1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_ReducingNesting"] = ("""
            (
            [
            ]
            )
            """, true, [
                new(LeftParen, "(", (0, 0), (0, 1)), new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(LeftSquareBracket, "[", (1, 0), (1, 1)), new(TriviaNewLine, "\n", (1, 1), (1, 2)),
                new(RightSquareBracket, "]", (2, 0), (2, 1)), new(TriviaNewLine, "\n", (2, 1), (2, 2) /* This token expected */),
                new(RightParen, ")", (3, 0), (3, 1)),
                eof(3, 1),
            ]),
            ["TriviaNewLine_RestoreLogicalNewLines"] = ("""
            (
            [
            ]
            )
            bau
            """, true, [
                new(LeftParen, "(", (0, 0), (0, 1)), new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(LeftSquareBracket, "[", (1, 0), (1, 1)), new(TriviaNewLine, "\n", (1, 1), (1, 2)),
                new(RightSquareBracket, "]", (2, 0), (2, 1)), new(TriviaNewLine, "\n", (2, 1), (2, 2)),
                new(RightParen, ")", (3, 0), (3, 1)), new(NewLine, "\n", (3, 1), (3, 2)),
                new(Name, "bau", (4, 0), (4, 3)),
                eof(4, 3),
            ]),
            ["WithoutTrivia_ReducingNesting"] = ("""
            (
            [
            ]
            )
            """, false, [
                new(LeftParen, "(", (0, 0), (0, 1)),
                new(LeftSquareBracket, "[", (1, 0), (1, 1)),
                new(RightSquareBracket, "]", (2, 0), (2, 1)),
                new(RightParen, ")", (3, 0), (3, 1)),
                eof(3, 1),
            ]),
            ["WithoutTrivia_RestoreLogicalNewLines"] = ("""
            (
            [
            ]
            )
            bau
            """, false, [
                new(LeftParen, "(", (0, 0), (0, 1)),
                new(LeftSquareBracket, "[", (1, 0), (1, 1)),
                new(RightSquareBracket, "]", (2, 0), (2, 1)),
                new(RightParen, ")", (3, 0), (3, 1)), new(NewLine, "\n", (3, 1), (3, 2)),
                new(Name, "bau", (4, 0), (4, 3)),
                eof(4, 3),
            ]),
            ["WithoutTrivia_NoIndents"] = ("""
            (
                bau
            )
            """, false, [
                new(LeftParen, "(", (0, 0), (0, 1)),
                new(Name, "bau", (1, 4), (1, 7)),
                new(RightParen, ")", (2, 0), (2, 1)),
                eof(2, 1),
            ]),
            ["TriviaWhiteSpace_NoIndents"] = ("""
            (
                bau
            )
            """, true, [
                new(LeftParen, "(", (0, 0), (0, 1)), new(TriviaNewLine, "\n", (0, 1), (0, 2)),
                new(WhiteSpace, "    ", (1, 0), (1, 4)), new(Name, "bau", (1, 4), (1, 7)), new(TriviaNewLine, "\n", (1, 7), (1, 8)),
                new(RightParen, ")", (2, 0), (2, 1)),
                eof(2, 1),
            ]),
        };

    /*
    Copy and paste this for new case.
            [""] = ("", [
                new(, "", (0, 0), ()),
                eof(),
            ]),
    */

    [Theory]
    [InlineData("AllInRow")]
    [InlineData("TriviaNewLine_Paren")]
    [InlineData("TriviaNewLine_Brace")]
    [InlineData("TriviaNewLine_SqBrt")]
    [InlineData("TriviaNewLine_ReducingNesting")]
    [InlineData("TriviaNewLine_RestoreLogicalNewLines")]
    [InlineData("WithoutTrivia_ReducingNesting")]
    [InlineData("WithoutTrivia_RestoreLogicalNewLines")]
    [InlineData("TriviaWhiteSpace_NoIndents")]
    [InlineData("WithoutTrivia_NoIndents")]
    public void TestBrackets(string @case)
    {
        Debug.Assert(parens_test_cases.ContainsKey(@case));

        (string code, bool trivia, var expected) = parens_test_cases[@case];

        test(code, expected, trivia: trivia);
    }

    private static void test(string code, IList<Token> expected, bool trivia = false)
    {
        var tokenizer = new PySharp.Tokenizer.Tokenizer(code, trivia);

        List<Token> result = [];
        Token token;
        do
        {
            token = tokenizer.ReadNext();
            result.Add(token);
        }
        while (token.Type is not EndOfFile);

        Assert.True(tokenizer.ShouldStop);

        Assert.Equal(expected.Count, result.Count);
        for (int i = 0; i < expected.Count; i++)
        {
            var exp = expected[i];
            var res = result[i];
            Assert.Equal(exp.Type, res.Type);
            Assert.Equal(exp.Lexeme, res.Lexeme);
            Assert.Equal(exp.Start, res.Start);
            Assert.Equal(exp.End, res.End);
        }
    }

    private static Token eof(int line, int col) =>
        new(EndOfFile, "", (line, col), (line, col));
}
