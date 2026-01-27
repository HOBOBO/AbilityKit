using System.Collections.Generic;
using Emilia.Reference;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式解析器（将Token列表解析为AST）
    /// </summary>
    public class ExpressionParser : IReference
    {
        private List<Token> _tokens;
        private int _position;
        private ExpressionConfig _config;

        public void Clear()
        {
            _tokens = null;
            _position = 0;
            _config = null;
        }

        /// <summary>
        /// 解析表达式
        /// </summary>
        /// <param name="tokens">Token列表</param>
        /// <param name="config">表达式配置</param>
        /// <returns>表达式AST</returns>
        public Expression Parse(List<Token> tokens, ExpressionConfig config)
        {
            if (tokens == null || tokens.Count == 0) throw new ExpressionParseException("表达式为空", 0);

            _tokens = tokens;
            _position = 0;
            _config = config;

            Expression result = ParseExpression();

            if (Current.type != TokenType.EOF) throw new ExpressionParseException($"意外的Token: {Current.value}", Current.position);

            return result;
        }

        private Token Current => _position < _tokens.Count ? _tokens[_position] : new Token(TokenType.EOF, "", -1);

        private Token Advance()
        {
            Token current = Current;
            _position++;
            return current;
        }

        private bool Match(TokenType type)
        {
            if (Current.type == type)
            {
                Advance();
                return true;
            }
            return false;
        }

        private void Expect(TokenType type, string message)
        {
            if (Current.type != type) throw new ExpressionParseException(message, Current.position);
            Advance();
        }

        /// <summary>
        /// 解析表达式
        /// </summary>
        private Expression ParseExpression() => ParsePrimary();

        /// <summary>
        /// 解析基本表达式
        /// </summary>
        private Expression ParsePrimary()
        {
            Token token = Current;

            switch (token.type)
            {
                case TokenType.Number:
                    return ParseNumber();

                case TokenType.String:
                    Advance();
                    return new LiteralExpression(LiteralType.String, token.value);

                case TokenType.Boolean:
                    Advance();
                    return new LiteralExpression(LiteralType.Boolean, token.value);

                case TokenType.Identifier:
                    return ParseIdentifier();

                case TokenType.LeftParen:
                    Advance();
                    Expression expr = ParseExpression();
                    Expect(TokenType.RightParen, "缺少 ')'");
                    return expr;

                default:
                    throw new ExpressionParseException($"意外的Token: {token.value}", token.position);
            }
        }

        /// <summary>
        /// 解析数字字面量
        /// </summary>
        private Expression ParseNumber()
        {
            Token token = Advance();
            string value = token.value;

            if (value.EndsWith("f") || value.EndsWith("F")) return new LiteralExpression(LiteralType.Float, value);

            if (value.EndsWith("d") || value.EndsWith("D")) return new LiteralExpression(LiteralType.Double, value);

            if (value.Contains(".") || value.Contains("e") || value.Contains("E")) return new LiteralExpression(LiteralType.Double, value);

            return new LiteralExpression(LiteralType.Integer, value);
        }

        /// <summary>
        /// 解析标识符（变量、常量或函数调用）
        /// </summary>
        private Expression ParseIdentifier()
        {
            Token token = Advance();
            string name = token.value;

            // 特殊关键字
            if (name == "null") return new LiteralExpression(LiteralType.Null, "null");

            // 函数调用
            if (Current.type == TokenType.LeftParen)
            {
                if (_config == null || ! _config.IsFunction(name)) throw new ExpressionParseException($"未定义的函数: {name}", token.position);

                Advance(); // 跳过 '('
                List<Expression> args = ParseArguments();
                Expect(TokenType.RightParen, "函数调用缺少 ')'");
                return new FunctionCallExpression(name, args);
            }

            // 常量
            if (_config != null && _config.IsConstant(name)) return new ConstantExpression(name);

            // 变量
            return new VariableExpression(name);
        }

        /// <summary>
        /// 解析函数参数列表
        /// </summary>
        private List<Expression> ParseArguments()
        {
            List<Expression> args = new();

            if (Current.type != TokenType.RightParen)
            {
                args.Add(ParseExpression());

                while (Match(TokenType.Comma))
                {
                    args.Add(ParseExpression());
                }
            }

            return args;
        }
    }
}