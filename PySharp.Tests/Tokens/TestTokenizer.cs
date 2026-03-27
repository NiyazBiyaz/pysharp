using System.Diagnostics;
using PySharp.Tokens;
using static PySharp.Tokens.TokenType;

namespace PySharp.Tests.Tokens;

public class TestTokenizer
{
    private static readonly Dictionary<string, (string code, IList<Token> expected)> one_row_test_cases =
        new()
        {
            ["String_SimpleSingle"] = ("'123'", [new(StringLiteral, "'123'", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["String_SimpleDouble"] = ("\"123\"", [new(StringLiteral, "\"123\"", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["String_SpaceBoth"] = ("'123' \"123\"", [new(StringLiteral, "'123'", p(0, 0), p(0, 5)),
                                                      new(StringLiteral, "\"123\"", p(0, 6), p(0, 11)), eof(0, 11)]),
            ["String_Empty"] = ("''", [new(StringLiteral, "''", p(0, 0), p(0, 2)), eof(0, 2)]),
            ["String_raw"] = ("r'123'", [new(StringLiteral, "r'123'", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["String_RAW"] = ("R'123'", [new(StringLiteral, "R'123'", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["String_byte"] = ("b'123'", [new(StringLiteral, "b'123'", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["String_BYTE"] = ("B'123'", [new(StringLiteral, "B'123'", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["String_RawByte"] = ("rb'123'", [new(StringLiteral, "rb'123'", p(0, 0), p(0, 7)), eof(0, 7)]),
            ["String_ByteRaw"] = ("br'123'", [new(StringLiteral, "br'123'", p(0, 0), p(0, 7)), eof(0, 7)]),
            ["String_EscapedQuote"] = (@"'bau\'bau'", [new(StringLiteral, @"'bau\'bau'", p(0, 0), p(0, 10)), eof(0, 10)]),

            ["Name_Simple"] = ("bau", [new(Name, "bau", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Name_Space"] = ("bau bau", [new(Name, "bau", p(0, 0), p(0, 3)), new(Name, "bau", p(0, 4), p(0, 7)), eof(0, 7)]),
            ["Name_TrailingUnderscore"] = ("baubau__", [new(Name, "baubau__", p(0, 0), p(0, 8)), eof(0, 8)]),
            ["Name_LeadingUnderscore"] = ("__bau", [new(Name, "__bau", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Name_BetweenUnderscore"] = ("__bau__", [new(Name, "__bau__", p(0, 0), p(0, 7)), eof(0, 7)]),
            ["Name_WithDigits"] = ("b123", [new(Name, "b123", p(0, 0), p(0, 4)), eof(0, 4)]),
            ["Name_Complex"] = ("bau_123_bau__", [new(Name, "bau_123_bau__", p(0, 0), p(0, 13)), eof(0, 13)]),

            ["Number_Simple"] = ("123", [new(Number, "123", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_Space"] = ("10 10", [new(Number, "10", p(0, 0), p(0, 2)), new(Number, "10", p(0, 3), p(0, 5)), eof(0, 5)]),
            ["Number_Zeros"] = ("000", [new(Number, "000", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_FractionZeros"] = ("0.0", [new(Number, "0.0", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_FractionLeadingZeros"] = ("01.10", [new(Number, "01.10", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_FractionLeadingZerosDot"] = ("01.", [new(Number, "01.", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_LeadingDot"] = (".10", [new(Number, ".10", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_TrailingDot"] = ("10.", [new(Number, "10.", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_SimpleExponent"] = ("10e2", [new(Number, "10e2", p(0, 0), p(0, 4)), eof(0, 4)]),
            ["Number_PlusExponent"] = ("10e+2", [new(Number, "10e+2", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_MinusExponent"] = ("10e-2", [new(Number, "10e-2", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_DotExponent"] = ("10.e2", [new(Number, "10.e2", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_SimpleImaginary"] = ("10j", [new(Number, "10j", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Number_ImaginaryAfterDot"] = ("10.j", [new(Number, "10.j", p(0, 0), p(0, 4)), eof(0, 4)]),
            ["Number_ImaginaryAfterExpo"] = ("10e1j", [new(Number, "10e1j", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_ImaginaryAfterDotExpo"] = ("10.e1j", [new(Number, "10.e1j", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["Number_NormalUnderscores"] = ("1_000_000", [new(Number, "1_000_000", p(0, 0), p(0, 9)), eof(0, 9)]),
            ["Number_WildUnderscores"] = ("10_0_0_0_0", [new(Number, "10_0_0_0_0", p(0, 0), p(0, 10)), eof(0, 10)]),
            ["Number_FractionUnderscores"] = ("1_1.1_1", [new(Number, "1_1.1_1", p(0, 0), p(0, 7)), eof(0, 7)]),
            ["Number_Complex1"] = ("1_2_3.1_2_3e+2j", [new(Number, "1_2_3.1_2_3e+2j", p(0, 0), p(0, 15)), eof(0, 15)]),
            ["Number_Complex2"] = (".1_2_3e-2j", [new(Number, ".1_2_3e-2j", p(0, 0), p(0, 10)), eof(0, 10)]),
            ["Number_Complex3"] = ("1_2_3.1_2_3e+2_0j", [new(Number, "1_2_3.1_2_3e+2_0j", p(0, 0), p(0, 17)), eof(0, 17)]),

            ["Number_Hexadecimal"] = ("0x123", [new(Number, "0x123", p(0, 0), p(0, 5)), eof(0, 5)]),
            ["Number_HexadecimalFullLower"] = ("0x0123456789abcdef", [new(Number, "0x0123456789abcdef", p(0, 0), p(0, 18)), eof(0, 18)]),
            ["Number_HexadecimalFullUpper"] = ("0x0123456789ABCDEF", [new(Number, "0x0123456789ABCDEF", p(0, 0), p(0, 18)), eof(0, 18)]),
            ["Number_HexadecimalMixed"] = ("0xAbCdEf", [new(Number, "0xAbCdEf", p(0, 0), p(0, 8)), eof(0, 8)]),
            ["Number_HexadecimalUnderscores"] = ("0x33_22_11", [new(Number, "0x33_22_11", p(0, 0), p(0, 10)), eof(0, 10)]),
            ["Number_HexadecimalLeadUnderscores"] = ("0x_33_22_11", [new(Number, "0x_33_22_11", p(0, 0), p(0, 11)), eof(0, 11)]),
            ["Number_OctalFull"] = ("0o01234567", [new(Number, "0o01234567", p(0, 0), p(0, 10)), eof(0, 10)]),
            ["Number_OctalUnderscores"] = ("0o_123_456_7", [new(Number, "0o_123_456_7", p(0, 0), p(0, 12)), eof(0, 12)]),
            ["Number_BinaryFull"] = ("0b01", [new(Number, "0b01", p(0, 0), p(0, 4)), eof(0, 4)]),
            ["Number_BinaryUnderscores"] = ("0b_1_0", [new(Number, "0b_1_0", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["Number_BinaryLong"] = ("0b101010100100100010101001", [new(Number, "0b101010100100100010101001", p(0, 0), p(0, 26)), eof(0, 26)]),

            // Please don't touch it. I spent a lot on these indexes...
            ["Op_AllExceptParensAndDots"] = (
                ", : := ; = == + += - -= -> * *= ** **= / /= // //= % %= & &= | |= @ @= ^ ^= ~ > >= >> >>= < <= << <<= ! !=",
                [
                    new(Comma,            ",",   p(0,   0),  p(0,   1)),
                    new(Colon,            ":",   p(0,   2),  p(0,   3)),
                    new(ColonEqual,       ":=",  p(0,   4),  p(0,   6)),
                    new(Semicolon,        ";",   p(0,   7),  p(0,   8)),
                    new(Equal,            "=",   p(0,   9),  p(0,  10)),
                    new(EqEqual,          "==",  p(0,  11),  p(0,  13)),
                    new(Plus,             "+",   p(0,  14),  p(0,  15)),
                    new(PlusEqual,        "+=",  p(0,  16),  p(0,  18)),
                    new(Minus,            "-",   p(0,  19),  p(0,  20)),
                    new(MinusEqual,       "-=",  p(0,  21),  p(0,  23)),
                    new(RightArrow,       "->",  p(0,  24),  p(0,  26)),
                    new(Star,             "*",   p(0,  27),  p(0,  28)),
                    new(StarEqual,        "*=",  p(0,  29),  p(0,  31)),
                    new(DoubleStar,       "**",  p(0,  32),  p(0,  34)),
                    new(DoubleStarEqual,  "**=", p(0,  35),  p(0,  38)),
                    new(Slash,            "/",   p(0,  39),  p(0,  40)),
                    new(SlashEqual,       "/=",  p(0,  41),  p(0,  43)),
                    new(DoubleSlash,      "//",  p(0,  44),  p(0,  46)),
                    new(DoubleSlashEqual, "//=", p(0,  47),  p(0,  50)),
                    new(Percent,          "%",   p(0,  51),  p(0,  52)),
                    new(PercentEqual,     "%=",  p(0,  53),  p(0,  55)),
                    new(Ampersand,        "&",   p(0,  56),  p(0,  57)),
                    new(AmpersandEqual,   "&=",  p(0,  58),  p(0,  60)),
                    new(VertBar,          "|",   p(0,  61),  p(0,  62)),
                    new(VertBarEqual,     "|=",  p(0,  63),  p(0,  65)),
                    new(At,               "@",   p(0,  66),  p(0,  67)),
                    new(AtEqual,          "@=",  p(0,  68),  p(0,  70)),
                    new(Circumflex,       "^",   p(0,  71),  p(0,  72)),
                    new(CircumflexEqual,  "^=",  p(0,  73),  p(0,  75)),
                    new(Tilde,            "~",   p(0,  76),  p(0,  77)),
                    new(Greater,          ">",   p(0,  78),  p(0,  79)),
                    new(GreaterEqual,     ">=",  p(0,  80),  p(0,  82)),
                    new(RightShift,       ">>",  p(0,  83),  p(0,  85)),
                    new(RightShiftEqual,  ">>=", p(0,  86),  p(0,  89)),
                    new(Less,             "<",   p(0,  90),  p(0,  91)),
                    new(LessEqual,        "<=",  p(0,  92),  p(0,  94)),
                    new(LeftShift,        "<<",  p(0,  95),  p(0,  97)),
                    new(LeftShiftEqual,   "<<=", p(0,  98),  p(0, 101)),
                    new(Exclamation,      "!",   p(0, 102),  p(0, 103)),
                    new(NotEqual,         "!=",  p(0, 104),  p(0, 106)),
                    eof(0, 106),
                ]
            ),
            // Since dots related to numbers they're separated.
            ["Op_Dot"] = (".", [new(Dot, ".", p(0, 0), p(0, 1)), eof(0, 1)]),
            ["Op_DotSpace"] = (". .", [new(Dot, ".", p(0, 0), p(0, 1)), new(Dot, ".", p(0, 2), p(0, 3)), eof(0, 3)]),
            ["Op_Ellipsis"] = ("...", [new(Ellipsis, "...", p(0, 0), p(0, 3)), eof(0, 3)]),
            ["Op_EllipsisSpace"] = ("... ...", [new(Ellipsis, "...", p(0, 0), p(0, 3)), new(Ellipsis, "...", p(0, 4), p(0, 7)), eof(0, 7)]),
            ["Op_EllipsisDot"] = ("... .", [new(Ellipsis, "...", p(0, 0), p(0, 3)), new(Dot, ".", p(0, 4), p(0, 5)), eof(0, 5)]),
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
            """, [new(StringLiteral, "'''bau'''", p(0, 0), p(0, 9)), eof(0, 9)]),
            ["MixedQuotes"] = ("''' \"\"\" '''", [new(StringLiteral, "''' \"\"\" '''", p(0, 0), p(0, 11)), eof(0, 11)]),
            ["EscapedQuote"] = ("""
            ''' \' '''
            """,
            [new(StringLiteral, "''' \\' '''", p(0, 0), p(0, 10)), eof(0, 10)]),
            ["OneLineFeed"] = ("""
            '''bau
            bau'''
            """, [new(StringLiteral, "'''bau\nbau'''", p(0, 0), p(1, 6)), eof(1, 6)]),
            ["EscapedEndOfLine"] = ("""
            '''bau\
            bau'''
            """, [new(StringLiteral, "'''bau\\\nbau'''", p(0, 0), p(1, 6)), eof(1, 6)]),
            ["LineFeedCRLF"] = ("'''bau\r\nbau'''", [new(StringLiteral, "'''bau\r\nbau'''", p(0, 0), p(1, 6)), eof(1, 6)]),
            ["LineFeedCR"] = ("'''bau\rbau'''", [new(StringLiteral, "'''bau\rbau'''", p(0, 0), p(1, 6)), eof(1, 6)]),
            ["Empty"] = ("''''''", [new(StringLiteral, "''''''", p(0, 0), p(0, 6)), eof(0, 6)]),
            ["Raw"] = (@"r'''b\au'''", [new(StringLiteral, @"r'''b\au'''", p(0, 0), p(0, 11)), eof(0, 11)]),
            ["Byte"] = (@"b'''r\au'''", [new(StringLiteral, @"b'''r\au'''", p(0, 0), p(0, 11)), eof(0, 11)]),
            ["ByteRaw"] = (@"rb'''me\ow'''", [new(StringLiteral, @"rb'''me\ow'''", p(0, 0), p(0, 13)), eof(0, 13)]),
            ["LongString"] = ("""
            '''bau1"
            bau2'
            bau3
            bau4'
            '''
            """,
            [new(StringLiteral, "'''bau1\"\nbau2'\nbau3\nbau4'\n'''", p(0, 0), p(4, 3)), eof(4, 3)]),
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
                new(LeftParen, "(", p(0, 0), p(0, 1)),
                new(LeftBrace, "{", p(0, 1), p(0, 2)),
                new(LeftSquareBracket, "[", p(0, 2), p(0, 3)),
                new(RightSquareBracket, "]", p(0, 3), p(0, 4)),
                new(RightBrace, "}", p(0, 4), p(0, 5)),
                new(RightParen, ")", p(0, 5), p(0, 6)),
                eof(0, 6),
            ]),
            ["TriviaNewLine_Paren"] = ("(\n)", true, [
                new(LeftParen, "(", p(0, 0), p(0, 1)),
                new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(RightParen, ")", p(1, 0), p(1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_Brace"] = ("{\n}", true, [
                new(LeftBrace, "{", p(0, 0), p(0, 1)),
                new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(RightBrace, "}", p(1, 0), p(1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_SqBrt"] = ("[\n]", true, [
                new(LeftSquareBracket, "[", p(0, 0), p(0, 1)),
                new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(RightSquareBracket, "]", p(1, 0), p(1, 1)),
                eof(1, 1),
            ]),
            ["TriviaNewLine_ReducingNesting"] = ("""
            (
            [
            ]
            )
            """, true, [
                new(LeftParen, "(", p(0, 0), p(0, 1)), new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(LeftSquareBracket, "[", p(1, 0), p(1, 1)), new(TriviaNewLine, "\n", p(1, 1), p(1, 2)),
                new(RightSquareBracket, "]", p(2, 0), p(2, 1)), new(TriviaNewLine, "\n", p(2, 1), p(2, 2) /* This token expected */),
                new(RightParen, ")", p(3, 0), p(3, 1)),
                eof(3, 1),
            ]),
            ["TriviaNewLine_RestoreLogicalNewLines"] = ("""
            (
            [
            ]
            )
            bau
            """, true, [
                new(LeftParen, "(", p(0, 0), p(0, 1)), new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(LeftSquareBracket, "[", p(1, 0), p(1, 1)), new(TriviaNewLine, "\n", p(1, 1), p(1, 2)),
                new(RightSquareBracket, "]", p(2, 0), p(2, 1)), new(TriviaNewLine, "\n", p(2, 1), p(2, 2)),
                new(RightParen, ")", p(3, 0), p(3, 1)), new(NewLine, "\n", p(3, 1), p(3, 2)),
                new(Name, "bau", p(4, 0), p(4, 3)),
                eof(4, 3),
            ]),
            ["WithoutTrivia_ReducingNesting"] = ("""
            (
            [
            ]
            )
            """, false, [
                new(LeftParen, "(", p(0, 0), p(0, 1)),
                new(LeftSquareBracket, "[", p(1, 0), p(1, 1)),
                new(RightSquareBracket, "]", p(2, 0), p(2, 1)),
                new(RightParen, ")", p(3, 0), p(3, 1)),
                eof(3, 1),
            ]),
            ["WithoutTrivia_RestoreLogicalNewLines"] = ("""
            (
            [
            ]
            )
            bau
            """, false, [
                new(LeftParen, "(", p(0, 0), p(0, 1)),
                new(LeftSquareBracket, "[", p(1, 0), p(1, 1)),
                new(RightSquareBracket, "]", p(2, 0), p(2, 1)),
                new(RightParen, ")", p(3, 0), p(3, 1)), new(NewLine, "\n", p(3, 1), p(3, 2)),
                new(Name, "bau", p(4, 0), p(4, 3)),
                eof(4, 3),
            ]),
            ["WithoutTrivia_NoIndents"] = ("""
            (
                bau
            )
            """, false, [
                new(LeftParen, "(", p(0, 0), p(0, 1)),
                new(Name, "bau", p(1, 4), p(1, 7)),
                new(RightParen, ")", p(2, 0), p(2, 1)),
                eof(2, 1),
            ]),
            ["TriviaWhiteSpace_NoIndents"] = ("""
            (
                bau
            )
            """, true, [
                new(LeftParen, "(", p(0, 0), p(0, 1)), new(TriviaNewLine, "\n", p(0, 1), p(0, 2)),
                new(WhiteSpace, "    ", p(1, 0), p(1, 4)), new(Name, "bau", p(1, 4), p(1, 7)), new(TriviaNewLine, "\n", p(1, 7), p(1, 8)),
                new(RightParen, ")", p(2, 0), p(2, 1)),
                eof(2, 1),
            ]),
        };

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

    private static readonly Dictionary<string, (string code, bool trivia, IList<Token> expected)> indentation_test_cases =
        new()
        {
            ["OneIndent"] = ("""
            bau
                bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(Dedent, empty,p(1, 7),p(1, 7)),
                eof(1, 7),
            ]),
            ["TwoIndents"] = ("""
            bau
                bau
                    bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Indent, "        ",p(2, 0),p(2, 8)), new(Name, "bau",p(2, 8),p(2, 11)),
                new(Dedent, empty,p(2, 11),p(2, 11)), new(Dedent, empty,p(2, 11),p(2, 11)),
                eof(2, 11),
            ]),
            ["OneDedent"] = ("""
            bau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Dedent, empty,p(2, 0),p(2, 0)), new(Name, "bau",p(2, 0),p(2, 3)),
                eof(2, 3),
            ]),
            ["TwoDedentSoft"] = ("""
            bau
                bau
                    bau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Indent, "        ",p(2, 0),p(2, 8)), new(Name, "bau",p(2, 8),p(2, 11)), new(NewLine, "\n",p(2, 11),p(2, 12)),
                new(Dedent, empty,p(3, 4),p(3, 4)), new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["TwoDedentHard"] = ("""
            bau
                bau
                    bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Indent, "        ",p(2, 0),p(2, 8)), new(Name, "bau",p(2, 8),p(2, 11)), new(NewLine, "\n",p(2, 11),p(2, 12)),
                new(Dedent, empty,p(3, 0),p(3, 0)), new(Dedent, empty,p(3, 0),p(3, 0)), new(Name, "bau",p(3, 0),p(3, 3)),
                eof(3, 3),
            ]),
            ["IndentAfterDedent"] = ("""
            bau
                bau
                    bau
                bau
                    bau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Indent, "        ",p(2, 0),p(2, 8)), new(Name, "bau",p(2, 8),p(2, 11)), new(NewLine, "\n",p(2, 11),p(2, 12)),
                new(Dedent, empty,p(3, 4),p(3, 4)), new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Indent, "        " /* Re-indent will have all chars from start to valued token */,p(4, 0),p(4, 8)),
                new(Name, "bau",p(4, 8),p(4, 11)), new(NewLine, "\n",p(4, 11),p(4, 12)),
                new(Dedent, empty,p(5, 4),p(5, 4)), new(Name, "bau",p(5, 4),p(5, 7)), new(NewLine, "\n",p(5, 7),p(5, 8)),
                new(Dedent, empty,p(6, 0),p(6, 0)), new(Name, "bau",p(6, 0),p(6, 3)),
                eof(6, 3),
            ]),
            ["HoldingIndentation"] = ("""
            bau
                bau
                bau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Name, "bau",p(2, 4),p(2, 7)), new(NewLine, "\n",p(2, 7),p(2, 8)),
                new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["HoldingIndentationWithSpace"] = ("""
            bau
                bau

                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["HoldingIndentationWithComment"] = ("""
            bau
                bau
                # baubau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["HoldingIndentationWithMoreNestedComment"] = ("""
            bau
                bau
                    # baubau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["HoldingIndentationWithLessNestedComment"] = ("""
            bau
                bau
            # baubau
                bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
                new(Name, "bau",p(3, 4),p(3, 7)), new(NewLine, "\n",p(3, 7),p(3, 8)),
                new(Dedent, empty,p(4, 0),p(4, 0)), new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["TabIndents"] = ("bau\n\tbau", false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "\t",p(1, 0),p(1, 1)), new(Name, "bau",p(1, 1),p(1, 4)),
                new(Dedent, empty,p(1, 4),p(1, 4)), eof(1, 4),
            ]),
            // No tests with form-feed, because in Python reference it's marked as UB
            // but they're supported and interprets as spaces.
            ["BigIndents"] = ("""
            bau
                    bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "        ",p(1, 0),p(1, 8)), new(Name, "bau",p(1, 8),p(1, 11)), new(NewLine, "\n",p(1, 11),p(1, 12)),
                new(Dedent, empty,p(2, 0),p(2, 0)), new(Name, "bau",p(2, 0),p(2, 3)),
                eof(2, 3),
            ]),
            ["MixedIndents"] = ("bau\n\t  bau\nbau", false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Indent, "\t  ",p(1, 0),p(1, 3)), new(Name, "bau",p(1, 3),p(1, 6)), new(NewLine, "\n",p(1, 6),p(1, 7)),
                new(Dedent, empty,p(2, 0),p(2, 0)), new(Name, "bau",p(2, 0),p(2, 3)),
                eof(2, 3),
            ]),
            ["Continuation_IndentDoesNotChanges"] = ("""
            bau\
                bau

                bau\
            bau
            bau
            """, false, [
            new(Name, "bau",p(0, 0),p(0, 3)),
            new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
            new(Indent, "    ",p(3, 0),p(3, 4)), new(Name, "bau",p(3, 4),p(3, 7)),
            new(Name, "bau",p(4, 0),p(4, 3)), new(NewLine, "\n",p(4, 3),p(4,4)),
            new(Dedent, empty,p(5,0),p(5,0)), new(Name, "bau",p(5,0),p(5, 3)),
            eof(5, 3),
            ]),
            ["Continuation_NewLineDoesNotGenerates"] = ("""
            bau\
            \
            \
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)),
                new(Name, "bau",p(3, 0),p(3, 3)),
                eof(3, 3),
            ]),
            ["ContinuationTrivia_IndentDoesNotChanges"] = ("""
            bau\
                bau

                bau\
            bau
            bau
            """, true, [
            new(Name, "bau",p(0, 0),p(0, 3)), new(BackSlash, "\\",p(0, 3),p(0, 4)), new(TriviaNewLine, "\n",p(0, 4),p(0, 5)),
            new(WhiteSpace, "    ",p(1, 0),p(1, 4)), new(Name, "bau",p(1, 4),p(1, 7)), new(NewLine, "\n",p(1, 7),p(1, 8)),
            new(TriviaNewLine, "\n",p(2, 0),p(2, 1)),
            new(Indent, "    ",p(3, 0),p(3, 4)), new(Name, "bau",p(3, 4),p(3, 7)), new(BackSlash, "\\",p(3, 7),p(3, 8)),
                    new(TriviaNewLine, "\n",p(3, 8),p(3, 9)),
            new(Name, "bau",p(4, 0),p(4, 3)), new(NewLine, "\n",p(4, 3),p(4,4)),
            new(Dedent, empty,p(5,0),p(5,0)), new(Name, "bau",p(5,0),p(5, 3)),
            eof(5, 3),
            ]),
            ["ContinuationTrivia_NewLineDoesNotGenerates"] = ("""
            bau\
            \
            \
            bau
            """, true, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(BackSlash, "\\",p(0, 3),p(0, 4)), new(TriviaNewLine, "\n",p(0, 4),p(0, 5)),
                new(BackSlash, "\\",p(1, 0),p(1, 1)), new(TriviaNewLine, "\n",p(1, 1),p(1, 2)),
                new(BackSlash, "\\",p(2, 0),p(2, 1)), new(TriviaNewLine, "\n",p(2, 1),p(2, 2)),
                new(Name, "bau",p(3, 0),p(3, 3)),
                eof(3, 3),
            ]),
            ["LineFeed_LF"] = ("bau\nbau", false, [
                new(Name, "bau",p(0, 0),p(0, 3)),
                new(NewLine, "\n",p(0, 3),p(0, 4)),
                new(Name, "bau",p(1, 0),p(1, 3)),
                eof(1, 3),
            ]),
            ["LineFeed_CR"] = ("bau\rbau", false, [
                new(Name, "bau",p(0, 0),p(0, 3)),
                new(NewLine, "\r",p(0, 3),p(0, 4)),
                new(Name, "bau",p(1, 0),p(1, 3)),
                eof(1, 3),
            ]),
            ["LineFeed_CRLF"] = ("bau\r\nbau", false, [
                new(Name, "bau",p(0, 0),p(0, 3)),
                new(NewLine, "\r\n",p(0, 3),p(0, 5)),
                new(Name, "bau",p(1, 0),p(1, 3)),
                eof(1, 3),
            ]),
            ["Comments"] = ("""
            bau # bau bau bau
            bau # bababau
            bau # bau~ bau~
            # bau
            bau
            """, false, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(NewLine, "\n",p(0, 17),p(0, 18)),
                new(Name, "bau",p(1, 0),p(1, 3)), new(NewLine, "\n",p(1, 13),p(1, 14)),
                new(Name, "bau",p(2, 0),p(2, 3)), new(NewLine, "\n",p(2, 15),p(2, 16)),
                new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
            ["CommentsTrivia"] = ("""
            bau # bau bau bau
            bau # bababau
            bau # bau~ bau~
            # bau
            bau
            """, true, [
                new(Name, "bau",p(0, 0),p(0, 3)), new(WhiteSpace, " ",p(0, 3),p(0, 4)),
                        new(Comment, "# bau bau bau",p(0, 4),p(0, 17)), new(NewLine, "\n",p(0, 17),p(0, 18)),
                new(Name, "bau",p(1, 0),p(1, 3)), new(WhiteSpace, " ",p(1, 3),p(1, 4)),
                        new(Comment, "# bababau",p(1, 4),p(1, 13)), new(NewLine, "\n",p(1, 13),p(1, 14)),
                new(Name, "bau",p(2, 0),p(2, 3)), new(WhiteSpace, " ",p(2, 3),p(2, 4)),
                        new(Comment, "# bau~ bau~",p(2, 4),p(2, 15)), new(NewLine, "\n",p(2, 15),p(2, 16)),
                new(Comment, "# bau",p(3, 0),p(3, 5)), new(TriviaNewLine, "\n",p(3, 5),p(3, 6)),
                new(Name, "bau",p(4, 0),p(4, 3)),
                eof(4, 3),
            ]),
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
    [InlineData("Comments")]
    [InlineData("CommentsTrivia")]
    public void TestIndentation(string @case)
    {
        Debug.Assert(indentation_test_cases.ContainsKey(@case));

        (string code, bool trivia, var expected) = indentation_test_cases[@case];

        test(code, expected, trivia: trivia);
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
                new(Error, "12baubau",p(0, 0),p(0, 8)),
                new(Plus, "+", p(0, 8), p(0, 9)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            // Valid token after error for make sure that error recovery works fine.
            // Invalid number always 8 chars width for copy-paste.
            ["Number_DoubleUnderscore_Integer"] =
            ("123__123 bau", dec_inv,
            [
                new(Error, "123__123",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_TrailingUnderscore_Integer"] =
            ("1231234_ bau", dec_inv,
            [
                new(Error, "1231234_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidTrailingChar_Integer"] =
            ("1231234i bau", dec_inv,
             [
                new(Error, "1231234i",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidChar_Integer"] =
            ("123bb123 bau", dec_inv,
            [
                new(Error, "123bb123",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_DoubleUnderscore_HexInt"] =
            ("0xff__ff bau", hex_inv,
            [
                new(Error, "0xff__ff",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_TrailingUnderscore_HexInt"] =
            ("0xfafa0_ bau", hex_inv,
            [
                new(Error, "0xfafa0_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidTrailingChar_HexInt"] =
            ("0xfaFa0h bau", hex_inv,
            [
                new(Error, "0xfaFa0h",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidChar_HexInt"] =
            ("0xfggFa0 bau", hex_inv,
            [
                new(Error, "0xfggFa0",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_DoubleUnderscore_OctInt"] =
            ("0o77__33 bau", oct_inv,
            [
                new(Error, "0o77__33",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_TrailingUnderscore_OctInt"] =
            ("0o12333_ bau", oct_inv,
            [
                new(Error, "0o12333_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidTrailingChar_OctInt"] =
            ("0o12312e bau", oct_inv,
            [
                new(Error, "0o12312e",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidChar_OctInt"] =
            ("0o138813 bau", oct_inv,
            [
                new(Error, "0o138813",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_LeadingZerosInInteger"] =
            ("00001234 bau", "Leading zeros in decimal integer are not permitted; use an '0o' prefix for octal numbers.",
            [
                new(Error, "00001234",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_DoubleUnderscore_BinInt"] =
            ("0b11__00 bau", bin_inv,
            [
                new(Error, "0b11__00",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_TrailingUnderscore_BinInt"] =
            ("0b10101_ bau", bin_inv,
            [
                new(Error, "0b10101_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidTrailingChar_BinInt"] =
            ("0b10101e bau", bin_inv,
            [
                new(Error, "0b10101e",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_InvalidChar_BinInt"] =
            ("0b101020 bau", bin_inv,
            [
                new(Error, "0b101020",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            // Float
            ["Number_Float_DoubleUnderscore"] =
            ("12.12__1 bau", dec_inv,
            [
                new(Error, "12.12__1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreBeforeDot"] =
            ("123_.123 bau", dec_inv,
            [
                new(Error, "123_.123",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_TrailingUnderscore"] =
            ("123.123_ bau", dec_inv,
            [
                new(Error, "123.123_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_InvalidChar"] =
            ("12a.1a23 bau", dec_inv,
            [
                new(Error, "12a.1a23",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_InvalidTrailingChar"] =
            ("123.123a bau", dec_inv,
            [
                new(Error, "123.123a",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_EmptyAfterE"] =
            ("123.233e bau", dec_inv,
            [
                new(Error, "123.233e",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreBeforeE"] =
            ("13.23_e1 bau", dec_inv,
            [
                new(Error, "13.23_e1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreAfterE"] =
            ("13.23e_1 bau", dec_inv,
            [
                new(Error, "13.23e_1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreBeforePlus"] = (
                "1.23e_+1 bau", dec_inv,
            [
                new(Error, "1.23e_+1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreAfterPlus"] =
            ("1.23e+_1 bau", dec_inv,
            [
                new(Error, "1.23e+_1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreBeforeMinus"] =
            ("1.23e_-1 bau", dec_inv,
            [
                new(Error, "1.23e_-1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Float_UnderscoreAfterMinus"] =
            ("1.23e-_1 bau", dec_inv,
            [
                new(Error, "1.23e-_1",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Imaginary_UnderscoreBeforeJ"] =
            ("123.13_j bau", img_inv,
            [
                new(Error, "123.13_j",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Imaginary_UnderscoreAfterJ"] =
            ("123.13j_ bau", img_inv,
            [
                new(Error, "123.13j_",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Imaginary_InvalidCharBeforeJ"] =
            ("123.13fj bau", img_inv,
            [
                new(Error, "123.13fj",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["Number_Imaginary_InvalidCharAfterJ"] =
            ("123.13jf bau", img_inv,
            [
                new(Error, "123.13jf",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            // Strings (F-/T-strings are separated)
            ["String_PrefixUR"] =
            ("ur\"bBau\" bau", string.Format(str_prf, "r", "u"),
            [
                new(Error, "ur\"bBau\"",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["String_PrefixUB"] =
            ("ub\"bBau\" bau", string.Format(str_prf, "b", "u"),
            [
                new(Error, "ub\"bBau\"",p(0, 0),p(0, 8)),
                new(Name, "bau",p(0, 9),p(0, 12)),
                eof(0, 12),
            ]),
            ["String_UnclosedOnLine_SingleQuote"] =
            // After unclosed string don't eat new line character
            ("\"baubau\nbau", unterminated,
            [
                new(Error, "\"baubau",p(0, 0),p(0, 7)), new(NewLine, "\n",p(0, 7),p(0, 8)),
                new(Name, "bau",p(1, 0),p(1, 3)),
                eof(1, 3),
            ]),
            ["String_UnclosedOnFile_SingleQuote"] =
            ("\"baubau", unterminated,
            [
                new(Error, "\"baubau",p(0, 0),p(0, 7)),
                eof(0, 7),
            ]),
            ["String_UnclosedOnFile_TripleQuote_SingleLine"] =
            ("'''baubaubaubaubau", unterminated,
            [
                new(Error, "'''baubaubaubaubau",p(0, 0),p(0, 18)),
                eof(0, 18),
            ]),
            ["String_UnclosedOnFile_TripleQuote_MultiLine"] =
            ("'''bauba\nbauba\nbau", unterminated,
            [
                new(Error, "'''bauba\nbauba\nbau",p(0, 0),p(2, 3)),
                eof(2, 3),
            ]),
            ["String_UnclosedOnFile_EscapedQuote"] =
            // '"bau\"bau'
            ("\"bau\\\"bau", "Unterminated string literal. Perhaps you escaped the end quote?",
            [
                new(Error, "\"bau\\\"bau",p(0, 0),p(0, 9)),
                eof(0, 9),
            ]),
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
    public void TestLiteralErrors(string @case)
    {
        Debug.Assert(literal_errors_test_cases.ContainsKey(@case));

        (string code, string message, var expected) = literal_errors_test_cases[@case];

        var tokenizer = test(code, expected);

        Assert.Equal(TokenizerError.InvalidLiteral, tokenizer.Error);
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
                new(Name, "bau", p(0, 0), p(0, 3)), new(NewLine, "\n", p(0, 3), p(0, 4)),
                new(Indent, "        ", p(1, 0), p(1, 8)), new(Name, "bau", p(1, 8), p(1, 11)), new(NewLine, "\n", p(1, 11), p(1, 12)),
                new(Error, empty, p(2, 0), p(2, 4)), new(Name, "bau", p(2, 4), p(2, 7)),
                // Error token replaces Dedent, so we releasing it is not needed.
                eof(2, 7),
            ]),
            ["AlternateIndentOnSameLevel"] = ("bau\n        bau\n\tbau", tabs,
            [
                new(Name, "bau", p(0, 0), p(0, 3)), new(NewLine, "\n", p(0, 3), p(0, 4)),
                new(Indent, "        ", p(1, 0), p(1, 8)), new(Name, "bau", p(1, 8), p(1, 11)), new(NewLine, "\n", p(1, 11), p(1, 12)),
                new(Error, empty, p(2, 0), p(2, 1)), new(Name, "bau", p(2, 1), p(2, 4)),
                new(Dedent, empty, p(2, 4), p(2, 4)), // Release indentation at the EOF.
                eof(2, 4),
            ]),
            ["AlternateIndentOnReducingLevel"] = ("bau\n        bau\n            bau\n\tbau", tabs,
            [
                new(Name, "bau", p(0, 0), p(0, 3)), new(NewLine, "\n", p(0, 3), p(0, 4)),
                new(Indent, "        ", p(1, 0), p(1, 8)), new(Name, "bau", p(1, 8), p(1, 11)), new(NewLine, "\n", p(1, 11), p(1, 12)),
                new(Indent, "            ", p(2, 0), p(2, 12)), new(Name, "bau", p(2, 12), p(2, 15)), new(NewLine, "\n", p(2, 15), p(2, 16)),
                new(Error, empty, p(3, 0), p(3, 1)), new(Name, "bau", p(3, 1), p(3, 4)),
                new(Dedent, empty, p(3, 4), p(3, 4)), // Release indentation at the EOF.
                eof(3, 4),
            ]),
            ["AlternateIndentOnIncreaseLevel"] = ("bau\n    bau\n\tbau", tabs,
            [
                new(Name, "bau", p(0, 0), p(0, 3)), new(NewLine, "\n", p(0, 3), p(0, 4)),
                new(Indent, "    ", p(1, 0), p(1, 4)), new(Name, "bau", p(1, 4), p(1, 7)), new(NewLine, "\n", p(1, 7), p(1, 8)),
                new(Error, empty, p(2, 0), p(2, 1)), new(Name, "bau", p(2, 1), p(2, 4)),
                new(Dedent, empty, p(2, 4), p(2, 4)), // Release indentation at the EOF.
                eof(2, 4)
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

        Debugger.Break();

        var tok = test(code, expected);

        Assert.Equal(TokenizerError.IndentationError, tok.Error);
        Assert.Equal(message, tok.ErrorMessage);
    }

    private static Tokenizer test(string code, IList<Token> expected, bool trivia = false)
    {
        var tokenizer = new Tokenizer(code, trivia);

        List<Token> result = [];
        Token token;
        do
        {
            token = tokenizer.ReadNext();
            result.Add(token);
        }
        while (token.Type is not EndOfFile && !tokenizer.ShouldStop);

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

        return tokenizer;
    }

    private static Token eof(int line, int col) =>
        new(EndOfFile, empty, new(line, col), new(line, col));

    private static TokenPosition p(int line, int column) => new(line, column);

    private static readonly ReadOnlyMemory<char> empty = ReadOnlyMemory<char>.Empty;
}
