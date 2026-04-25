namespace PySharp.Tokens;

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
    }
}
