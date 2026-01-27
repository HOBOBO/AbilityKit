namespace Emilia.Expressions
{
    public enum TokenType
    {
        Number,
        String,
        Boolean,
        Identifier,
        LeftParen,
        RightParen,
        Comma,
        EOF
    }

    public struct Token
    {
        public TokenType type;
        public string value;
        public int position;

        public Token(TokenType type, string value, int position)
        {
            this.type = type;
            this.value = value;
            this.position = position;
        }
    }
}