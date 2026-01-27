using System;
using System.Collections.Generic;
using System.Text;
using Emilia.Reference;

namespace Emilia.Expressions
{
    /// <summary>
    /// 表达式词法分析器
    /// </summary>
    public class ExpressionTokenizer : IReference
    {
        private string _input;
        private int _position;
        private int _length;

        public void Clear()
        {
            _input = null;
            _position = 0;
            _length = 0;
        }

        /// <summary>
        /// 对输入字符串进行词法分析
        /// </summary>
        /// <param name="input">输入表达式</param>
        /// <returns>Token列表</returns>
        public List<Token> Tokenize(string input)
        {
            if (string.IsNullOrEmpty(input)) return new List<Token>();

            _input = input;
            _position = 0;
            _length = input.Length;

            List<Token> tokens = new();

            while (_position < _length)
            {
                char current = _input[_position];

                // 跳过空白字符
                if (char.IsWhiteSpace(current))
                {
                    _position++;
                    continue;
                }

                // 数字（包括负数）
                if (char.IsDigit(current) || (current == '.' && _position + 1 < _length && char.IsDigit(_input[_position + 1])))
                {
                    tokens.Add(ReadNumber());
                    continue;
                }

                // 字符串
                if (current == '"' || current == '\'')
                {
                    tokens.Add(ReadString(current));
                    continue;
                }

                // 标识符或关键字
                if (char.IsLetter(current) || current == '_')
                {
                    tokens.Add(ReadIdentifier());
                    continue;
                }

                // 括号和分隔符
                if (current == '(')
                {
                    tokens.Add(new Token(TokenType.LeftParen, "(", _position++));
                    continue;
                }

                if (current == ')')
                {
                    tokens.Add(new Token(TokenType.RightParen, ")", _position++));
                    continue;
                }

                if (current == ',')
                {
                    tokens.Add(new Token(TokenType.Comma, ",", _position++));
                    continue;
                }

                throw new ExpressionParseException($"未知字符 '{current}'", _position);
            }

            tokens.Add(new Token(TokenType.EOF, "", _position));
            return tokens;
        }

        private Token ReadNumber()
        {
            int start = _position;
            StringBuilder sb = new();
            bool hasDecimal = false;
            bool hasExponent = false;

            while (_position < _length)
            {
                char c = _input[_position];

                if (char.IsDigit(c))
                {
                    sb.Append(c);
                    _position++;
                }
                else if (c == '.' && ! hasDecimal && ! hasExponent)
                {
                    hasDecimal = true;
                    sb.Append(c);
                    _position++;
                }
                else if ((c == 'e' || c == 'E') && ! hasExponent)
                {
                    hasExponent = true;
                    sb.Append(c);
                    _position++;

                    // 处理指数符号
                    if (_position < _length && (_input[_position] == '+' || _input[_position] == '-'))
                    {
                        sb.Append(_input[_position]);
                        _position++;
                    }
                }
                else if (c == 'f' || c == 'F' || c == 'd' || c == 'D')
                {
                    sb.Append(c);
                    _position++;
                    break;
                }
                else
                {
                    break;
                }
            }

            return new Token(TokenType.Number, sb.ToString(), start);
        }

        private Token ReadString(char quote)
        {
            int start = _position;
            _position++; // 跳过开始引号

            StringBuilder sb = new();

            while (_position < _length)
            {
                char c = _input[_position];

                if (c == quote)
                {
                    _position++; // 跳过结束引号
                    return new Token(TokenType.String, sb.ToString(), start);
                }

                if (c == '\\' && _position + 1 < _length)
                {
                    _position++;
                    char escaped = _input[_position];
                    switch (escaped)
                    {
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case '"':
                            sb.Append('"');
                            break;
                        case '\'':
                            sb.Append('\'');
                            break;
                        default:
                            sb.Append(escaped);
                            break;
                    }

                    _position++;
                }
                else
                {
                    sb.Append(c);
                    _position++;
                }
            }

            throw new ExpressionParseException("字符串未闭合", start);
        }

        private Token ReadIdentifier()
        {
            int start = _position;
            StringBuilder sb = new();

            while (_position < _length)
            {
                char c = _input[_position];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    sb.Append(c);
                    _position++;
                }
                else
                {
                    break;
                }
            }

            string value = sb.ToString();

            // 检查是否为布尔关键字
            if (value == "true" || value == "false")
            {
                return new Token(TokenType.Boolean, value, start);
            }

            return new Token(TokenType.Identifier, value, start);
        }
    }

    /// <summary>
    /// 表达式解析异常
    /// </summary>
    public class ExpressionParseException : Exception
    {
        public int Position { get; }

        public ExpressionParseException(string message, int position) : base($"{message} (位置: {position})")
        {
            Position = position;
        }
    }
}