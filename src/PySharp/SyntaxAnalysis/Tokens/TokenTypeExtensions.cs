namespace PySharp.SyntaxAnalysis.Tokens;

public static class TokenTypeExtensions
{
    extension(TokenType type)
    {
        public bool IsTrivia => type switch
        {
            TokenType.TriviaNewLine or
            TokenType.WhiteSpace or
            TokenType.BackSlash or
            TokenType.Comment => true,
            _ => false,
        };
        public bool IsError => type == TokenType.Error;

        public static bool TryGetDelimiterByString(ReadOnlySpan<char> value, out TokenType tokenType)
        {
            var spanLookup = delimiters.GetAlternateLookup<ReadOnlySpan<char>>();
            if (spanLookup.TryGetValue(value, out var tt))
            {
                tokenType = tt;
                return true;
            }

            tokenType = default;
            return false;
        }

        public static bool IsReserved(ReadOnlySpan<char> value)
        {
            var spanLookup = reserved_names.GetAlternateLookup<ReadOnlySpan<char>>();
            return spanLookup.Contains(value);
        }

        public Token GetStandardToken() => type switch
        {
            // Tokens with variadic lexemes.
            TokenType.Name or
            TokenType.Number or
            TokenType.StringLiteral or
            TokenType.FStringStart or
            TokenType.FStringMiddle or
            TokenType.FStringEnd or
            TokenType.TStringStart or
            TokenType.TStringMiddle or
            TokenType.TStringEnd or
            TokenType.DebugSpecifierString or
            TokenType.Comment or
            TokenType.WhiteSpace or
            TokenType.Indent or
            TokenType.Dedent or
            TokenType.NewLine or
            TokenType.TriviaNewLine or
            TokenType.Error => throw new ArgumentOutOfRangeException(nameof(type), "This token type have variadic lexeme and cannot be created in standard way."),

            TokenType.Ampersand => new(type, "&"),
            TokenType.AmpersandEqual => new(type, "&="),
            TokenType.At => new(type, "@"),
            TokenType.AtEqual => new(type, "@="),
            TokenType.BackSlash => new(type, "\\"),
            TokenType.CircumflexEqual => new(type, "^="),
            TokenType.Circumflex => new(type, "^"),
            TokenType.Colon => new(type, ":"),
            TokenType.ColonEqual => new(type, ":="),
            TokenType.Comma => new(type, ","),
            TokenType.Dot => new(type, "."),
            TokenType.DoubleSlash => new(type, "//"),
            TokenType.DoubleSlashEqual => new(type, "//="),
            TokenType.DoubleStar => new(type, "**"),
            TokenType.DoubleStarEqual => new(type, "**="),
            TokenType.Ellipsis => new(type, "..."),
            TokenType.EndOfFile => new(type, ""),
            TokenType.EqEqual => new(type, "=="),
            TokenType.Equal => new(type, "="),
            TokenType.Exclamation => new(type, "!"),
            TokenType.Greater => new(type, ">"),
            TokenType.GreaterEqual => new(type, ">="),
            TokenType.LeftBrace => new(type, "{"),
            TokenType.LeftParen => new(type, "("),
            TokenType.LeftShift => new(type, "<<"),
            TokenType.LeftShiftEqual => new(type, "<<="),
            TokenType.LeftSquareBracket => new(type, "["),
            TokenType.Less => new(type, "<"),
            TokenType.LessEqual => new(type, "<="),
            TokenType.Minus => new(type, "-"),
            TokenType.MinusEqual => new(type, "-="),
            TokenType.NotEqual => new(type, "!="),
            TokenType.Percent => new(type, "%"),
            TokenType.PercentEqual => new(type, "%="),
            TokenType.Plus => new(type, "+"),
            TokenType.PlusEqual => new(type, "+="),
            TokenType.RightArrow => new(type, "->"),
            TokenType.RightBrace => new(type, "}"),
            TokenType.RightParen => new(type, ")"),
            TokenType.RightShift => new(type, ">>"),
            TokenType.RightShiftEqual => new(type, ">>="),
            TokenType.RightSquareBracket => new(type, "]"),
            TokenType.Semicolon => new(type, ";"),
            TokenType.Slash => new(type, "/"),
            TokenType.SlashEqual => new(type, "/="),
            TokenType.Star => new(type, "*"),
            TokenType.StarEqual => new(type, "*="),
            TokenType.Tilde => new(type, "~"),
            TokenType.VertBar => new(type, "|"),
            TokenType.VertBarEqual => new(type, "|="),

            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static readonly HashSet<string> reserved_names = [.. Enum.GetNames<TokenType>()];
    private static readonly Dictionary<string, TokenType> delimiters = new()
    {
        ["..."] = TokenType.Ellipsis,
        ["("] = TokenType.LeftParen,
        [")"] = TokenType.RightParen,
        ["["] = TokenType.LeftSquareBracket,
        ["]"] = TokenType.RightSquareBracket,
        ["{"] = TokenType.LeftBrace,
        ["}"] = TokenType.RightBrace,
        ["."] = TokenType.Dot,
        [","] = TokenType.Comma,
        [":"] = TokenType.Colon,
        [":="] = TokenType.ColonEqual,
        [";"] = TokenType.Semicolon,
        ["="] = TokenType.Equal,
        ["=="] = TokenType.EqEqual,
        ["+"] = TokenType.Plus,
        ["+="] = TokenType.PlusEqual,
        ["-"] = TokenType.Minus,
        ["-="] = TokenType.MinusEqual,
        ["->"] = TokenType.RightArrow,
        ["*"] = TokenType.Star,
        ["*="] = TokenType.StarEqual,
        ["**"] = TokenType.DoubleStar,
        ["**="] = TokenType.DoubleStarEqual,
        ["/"] = TokenType.Slash,
        ["/="] = TokenType.SlashEqual,
        ["//"] = TokenType.DoubleSlash,
        ["//="] = TokenType.DoubleSlashEqual,
        ["%"] = TokenType.Percent,
        ["%="] = TokenType.PercentEqual,
        ["&"] = TokenType.Ampersand,
        ["&="] = TokenType.AmpersandEqual,
        ["|"] = TokenType.VertBar,
        ["|="] = TokenType.VertBarEqual,
        ["@"] = TokenType.At,
        ["@="] = TokenType.AtEqual,
        ["^"] = TokenType.Circumflex,
        ["^="] = TokenType.CircumflexEqual,
        ["~"] = TokenType.Tilde,
        [">"] = TokenType.Greater,
        [">="] = TokenType.GreaterEqual,
        [">>"] = TokenType.RightShift,
        [">>="] = TokenType.RightShiftEqual,
        ["<"] = TokenType.Less,
        ["<="] = TokenType.LessEqual,
        ["<<"] = TokenType.LeftShift,
        ["<<="] = TokenType.LeftShiftEqual,
        ["!"] = TokenType.Exclamation,
        ["!="] = TokenType.NotEqual,
    };
}
