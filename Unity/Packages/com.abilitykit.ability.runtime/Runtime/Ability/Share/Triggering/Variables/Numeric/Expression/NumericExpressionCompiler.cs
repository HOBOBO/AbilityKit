using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    public static class NumericExpressionCompiler
    {
        private static readonly Dictionary<string, NumericRpnProgram> _cache = new Dictionary<string, NumericRpnProgram>(StringComparer.Ordinal);
        private static readonly object _cacheLock = new object();

        private enum Op
        {
            Add,
            Sub,
            Mul,
            Div,
            LParen
        }

        public static bool TryCompileCached(string expr, out NumericRpnProgram program)
        {
            program = null;
            if (string.IsNullOrWhiteSpace(expr)) return false;

            lock (_cacheLock)
            {
                if (_cache.TryGetValue(expr, out program) && program != null) return true;
            }

            if (!TryCompile(expr, out program) || program == null) return false;

            lock (_cacheLock)
            {
                _cache[expr] = program;
            }

            return true;
        }

        public static bool TryCompile(string expr, out NumericRpnProgram program)
        {
            program = null;
            if (string.IsNullOrWhiteSpace(expr)) return false;

            var output = new List<NumericRpnToken>(16);
            var ops = new Stack<Op>(16);

            var i = 0;
            var prevWasValue = false;

            while (i < expr.Length)
            {
                var c = expr[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // number
                if (char.IsDigit(c) || c == '.')
                {
                    if (!TryReadNumber(expr, ref i, out var num)) return false;
                    output.Add(NumericRpnToken.NumberToken(num));
                    prevWasValue = true;
                    continue;
                }

                // identifier: domain.key (required)
                if (IsIdentStart(c))
                {
                    if (!TryReadIdentifier(expr, ref i, out var ident)) return false;

                    // function call: name(...)
                    if (i < expr.Length && expr[i] == '(')
                    {
                        // parse call arguments with recursion by compiling each arg into output then append func token.
                        if (!TryParseFunctionCall(expr, ref i, ident, output, ops)) return false;
                        prevWasValue = true;
                        continue;
                    }

                    var dot = ident.IndexOf('.');
                    if (dot <= 0 || dot >= ident.Length - 1) return false;
                    var domainId = ident.Substring(0, dot);
                    var key = ident.Substring(dot + 1);
                    output.Add(NumericRpnToken.VarToken(domainId, key));
                    prevWasValue = true;
                    continue;
                }

                if (c == '(')
                {
                    ops.Push(Op.LParen);
                    i++;
                    prevWasValue = false;
                    continue;
                }

                if (c == ')')
                {
                    // pop until lparen
                    var found = false;
                    while (ops.Count > 0)
                    {
                        var op = ops.Pop();
                        if (op == Op.LParen)
                        {
                            found = true;
                            break;
                        }
                        EmitOp(output, op);
                    }
                    if (!found) return false;
                    i++;
                    prevWasValue = true;
                    continue;
                }

                if (IsOperator(c))
                {
                    // unary minus => 0 - x
                    if (c == '-' && !prevWasValue)
                    {
                        output.Add(NumericRpnToken.NumberToken(0d));
                    }

                    var op = ToOp(c);
                    while (ops.Count > 0)
                    {
                        var top = ops.Peek();
                        if (top == Op.LParen) break;
                        if (Precedence(top) >= Precedence(op))
                        {
                            EmitOp(output, ops.Pop());
                            continue;
                        }
                        break;
                    }
                    ops.Push(op);
                    i++;
                    prevWasValue = false;
                    continue;
                }

                return false;
            }

            while (ops.Count > 0)
            {
                var op = ops.Pop();
                if (op == Op.LParen) return false;
                EmitOp(output, op);
            }

            if (output.Count == 0) return false;

            program = new NumericRpnProgram(expr, output.ToArray());
            return true;
        }

        private static bool TryParseFunctionCall(string expr, ref int i, string funcName, List<NumericRpnToken> output, Stack<Op> ops)
        {
            // current char is '('
            if (i >= expr.Length || expr[i] != '(') return false;
            i++; // consume '('

            var argCount = 0;
            var start = i;
            var depth = 0;

            while (i < expr.Length)
            {
                var c = expr[i];
                if (c == '(') depth++;
                else if (c == ')')
                {
                    if (depth == 0)
                    {
                        // last arg
                        if (!TryCompileSegment(expr, start, i - start, output)) return false;
                        if (i > start || argCount > 0) argCount++;
                        i++; // consume ')'
                        output.Add(NumericRpnToken.FuncToken(funcName, argCount));
                        return true;
                    }
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    if (!TryCompileSegment(expr, start, i - start, output)) return false;
                    argCount++;
                    i++; // consume ','
                    start = i;
                    continue;
                }

                i++;
            }

            return false;
        }

        private static bool TryCompileSegment(string expr, int start, int length, List<NumericRpnToken> output)
        {
            var seg = expr.Substring(start, length).Trim();
            if (seg.Length == 0) return false;

            if (!TryCompile(seg, out var program)) return false;
            var tokens = program.Tokens;
            for (int ti = 0; ti < tokens.Length; ti++) output.Add(tokens[ti]);
            return true;
        }

        private static bool TryReadNumber(string expr, ref int i, out double value)
        {
            value = 0d;
            var start = i;
            while (i < expr.Length)
            {
                var c = expr[i];
                if (char.IsDigit(c) || c == '.')
                {
                    i++;
                    continue;
                }
                break;
            }

            if (i <= start) return false;

            var s = expr.Substring(start, i - start);
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadIdentifier(string expr, ref int i, out string ident)
        {
            ident = null;
            var start = i;
            i++;
            while (i < expr.Length)
            {
                var c = expr[i];
                if (IsIdentPart(c) || c == '.')
                {
                    i++;
                    continue;
                }
                break;
            }

            ident = expr.Substring(start, i - start);
            return !string.IsNullOrEmpty(ident);
        }

        private static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsIdentPart(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static bool IsOperator(char c) => c == '+' || c == '-' || c == '*' || c == '/';

        private static Op ToOp(char c)
        {
            if (c == '+') return Op.Add;
            if (c == '-') return Op.Sub;
            if (c == '*') return Op.Mul;
            return Op.Div;
        }

        private static int Precedence(Op op)
        {
            if (op == Op.Mul || op == Op.Div) return 2;
            if (op == Op.Add || op == Op.Sub) return 1;
            return 0;
        }

        private static void EmitOp(List<NumericRpnToken> output, Op op)
        {
            if (op == Op.Add) output.Add(NumericRpnToken.AddToken());
            else if (op == Op.Sub) output.Add(NumericRpnToken.SubToken());
            else if (op == Op.Mul) output.Add(NumericRpnToken.MulToken());
            else if (op == Op.Div) output.Add(NumericRpnToken.DivToken());
        }
    }
}
