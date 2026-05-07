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

        public static bool TryGetDelimiterByString(string value, out TokenType tokenType)
        {
            if (delimiters.TryGetValue(value, out var tt))
            {
                tokenType = tt;
                return true;
            }

            tokenType = default;
            return false;
        }
    }
    private static readonly IReadOnlyDictionary<string, TokenType> delimiters = new Dictionary<string, TokenType>()
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
