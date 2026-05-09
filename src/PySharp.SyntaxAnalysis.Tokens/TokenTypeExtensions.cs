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
