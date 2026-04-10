using System;
using System.Collections.Generic;
using System.Globalization;
using AbilityKit.Modifiers;

namespace AbilityKit.Ability.Share.Common.AttributeSystem
{
    // ============================================================================
    // 表达式属性公式
    // ============================================================================

    /// <summary>
    /// 表达式属性公式。
    /// 支持自定义计算表达式，如 "base * 2 + strength * 0.5"
    ///
    /// 支持的内置变量：
    /// - base: 基础值
    /// - add: 加法值之和
    /// - mul: 乘法值之和（MulProduct - 1）
    /// - override: 覆盖值
    /// - hasOverride: 是否有覆盖（0 或 1）
    ///
    /// 支持的函数：
    /// - abs(x): 绝对值
    /// - min(a, b): 最小值
    /// - max(a, b): 最大值
    /// - clamp(x, min, max): 限制范围
    /// </summary>
    public sealed class AttributeExpressionFormula : IAttributeFormula, IAttributeDependencyProvider
    {
        private readonly string _expr;

        private bool _parsed;
        private Instruction[] _program;
        private AttributeId[] _deps;

        public AttributeExpressionFormula(string expr)
        {
            _expr = expr ?? throw new ArgumentNullException(nameof(expr));
        }

        public IEnumerable<AttributeId> GetDependencies()
        {
            EnsureParsed(self: default);
            return _deps;
        }

        /// <summary>
        /// 评估属性值。
        /// </summary>
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult modifierResult)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (!self.IsValid) throw new ArgumentException("Invalid AttributeId", nameof(self));

            EnsureParsed(self);

            var stack = new float[32];
            var sp = 0;

            static void Push(ref float[] arr, ref int sp2, float v)
            {
                if (sp2 >= arr.Length)
                {
                    Array.Resize(ref arr, arr.Length * 2);
                }
                arr[sp2++] = v;
            }

            static float Pop(float[] arr, ref int sp2)
            {
                if (sp2 <= 0) throw new InvalidOperationException("Expression stack underflow");
                return arr[--sp2];
            }

            for (int i = 0; i < _program.Length; i++)
            {
                var ins = _program[i];
                switch (ins.Op)
                {
                    case OpCode.Const:
                        Push(ref stack, ref sp, ins.Const);
                        break;

                    case OpCode.VarBuiltin:
                        Push(ref stack, ref sp, ResolveBuiltin(ins.Builtin, baseValue, modifierResult));
                        break;

                    case OpCode.VarAttr:
                        Push(ref stack, ref sp, ctx.GetValue(ins.Attr));
                        break;

                    case OpCode.Add:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, a + b);
                        break;
                    }

                    case OpCode.Sub:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, a - b);
                        break;
                    }

                    case OpCode.Mul:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, a * b);
                        break;
                    }

                    case OpCode.Div:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, a / b);
                        break;
                    }

                    case OpCode.Neg:
                    {
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, -a);
                        break;
                    }

                    case OpCode.FuncAbs:
                    {
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, System.Math.Abs(a));
                        break;
                    }

                    case OpCode.FuncMin:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, System.Math.Min(a, b));
                        break;
                    }

                    case OpCode.FuncMax:
                    {
                        var b = Pop(stack, ref sp);
                        var a = Pop(stack, ref sp);
                        Push(ref stack, ref sp, System.Math.Max(a, b));
                        break;
                    }

                    case OpCode.FuncClamp:
                    {
                        var hi = Pop(stack, ref sp);
                        var lo = Pop(stack, ref sp);
                        var x = Pop(stack, ref sp);
                        Push(ref stack, ref sp, Clamp(x, lo, hi));
                        break;
                    }

                    default:
                        throw new InvalidOperationException($"Unsupported opcode: {ins.Op}");
                }
            }

            if (sp != 1) throw new InvalidOperationException("Expression evaluation error: stack not balanced");

            var v = stack[0];
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            return v;
        }

        private void EnsureParsed(AttributeId self)
        {
            if (_parsed) return;

            var parsed = Parser.Parse(_expr, self);
            _program = parsed.Program;
            _deps = parsed.Dependencies;
            _parsed = true;
        }

        private static float Clamp(float x, float lo, float hi)
        {
            if (x < lo) return lo;
            if (x > hi) return hi;
            return x;
        }

        private static float ResolveBuiltin(BuiltinVar v, float baseValue, ModifierResult modifierResult)
        {
            switch (v)
            {
                case BuiltinVar.Base: return baseValue;
                case BuiltinVar.Add: return modifierResult.AddSum;
                case BuiltinVar.Mul: return modifierResult.MulProduct - 1f;
                case BuiltinVar.Override: return modifierResult.OverrideValue;
                case BuiltinVar.HasOverride: return modifierResult.HasOverride ? 1f : 0f;
                default: return 0f;
            }
        }

        private enum BuiltinVar
        {
            Base = 0,
            Add = 1,
            Mul = 2,
            Override = 4,
            HasOverride = 5,
        }

        private enum OpCode
        {
            Const = 0,
            VarBuiltin = 1,
            VarAttr = 2,

            Add = 10,
            Sub = 11,
            Mul = 12,
            Div = 13,
            Neg = 14,

            FuncAbs = 30,
            FuncMin = 31,
            FuncMax = 32,
            FuncClamp = 33,
        }

        private readonly struct Instruction
        {
            public readonly OpCode Op;
            public readonly float Const;
            public readonly AttributeId Attr;
            public readonly BuiltinVar Builtin;

            private Instruction(OpCode op, float c, AttributeId attr, BuiltinVar builtin)
            {
                Op = op;
                Const = c;
                Attr = attr;
                Builtin = builtin;
            }

            public static Instruction C(float v) => new Instruction(OpCode.Const, v, default, default);
            public static Instruction B(BuiltinVar v) => new Instruction(OpCode.VarBuiltin, 0f, default, v);
            public static Instruction A(AttributeId id) => new Instruction(OpCode.VarAttr, 0f, id, default);
            public static Instruction Op1(OpCode op) => new Instruction(op, 0f, default, default);
        }

        private readonly struct Parsed
        {
            public readonly Instruction[] Program;
            public readonly AttributeId[] Dependencies;

            public Parsed(Instruction[] program, AttributeId[] deps)
            {
                Program = program;
                Dependencies = deps;
            }
        }

        private static class Parser
        {
            private enum TokenKind
            {
                End = 0,
                Number,
                Ident,
                Plus,
                Minus,
                Star,
                Slash,
                LParen,
                RParen,
                Comma,
            }

            private readonly struct Token
            {
                public readonly TokenKind Kind;
                public readonly string Text;
                public readonly float Number;

                public Token(TokenKind kind, string text, float number)
                {
                    Kind = kind;
                    Text = text;
                    Number = number;
                }
            }

            private enum StackItemKind
            {
                Op,
                Func,
                LParen,
            }

            private readonly struct StackItem
            {
                public readonly StackItemKind Kind;
                public readonly TokenKind Op;
                public readonly string Func;
                public readonly bool Unary;

                public StackItem(StackItemKind kind, TokenKind op, string func, bool unary)
                {
                    Kind = kind;
                    Op = op;
                    Func = func;
                    Unary = unary;
                }

                public static StackItem L() => new StackItem(StackItemKind.LParen, default, null, false);
                public static StackItem O(TokenKind op, bool unary) => new StackItem(StackItemKind.Op, op, null, unary);
                public static StackItem F(string name) => new StackItem(StackItemKind.Func, default, name, false);
            }

            public static Parsed Parse(string expr, AttributeId self)
            {
                if (expr == null) throw new ArgumentNullException(nameof(expr));

                var tokens = Tokenize(expr);
                var output = new List<Instruction>(64);
                var stack = new List<StackItem>(32);
                var deps = new HashSet<int>();

                var prevWasValue = false;
                for (int i = 0; i < tokens.Count; i++)
                {
                    var t = tokens[i];
                    switch (t.Kind)
                    {
                        case TokenKind.Number:
                            output.Add(Instruction.C(t.Number));
                            prevWasValue = true;
                            break;

                        case TokenKind.Ident:
                            {
                                var nextIsLParen = i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.LParen;
                                if (nextIsLParen)
                                {
                                    stack.Add(StackItem.F(t.Text));
                                    prevWasValue = false;
                                    break;
                                }

                                if (TryBuiltin(t.Text, out var b))
                                {
                                    output.Add(Instruction.B(b));
                                    prevWasValue = true;
                                    break;
                                }

                                if (!AttributeRegistry.Instance.TryGet(t.Text, out var attrId))
                                {
                                    throw new InvalidOperationException($"Attribute not registered: {t.Text}");
                                }

                                if (self.IsValid && attrId == self)
                                {
                                    throw new InvalidOperationException($"Expression cannot reference itself: {t.Text}");
                                }

                                deps.Add(attrId.Id);
                                output.Add(Instruction.A(attrId));
                                prevWasValue = true;
                                break;
                            }

                        case TokenKind.LParen:
                            stack.Add(StackItem.L());
                            prevWasValue = false;
                            break;

                        case TokenKind.RParen:
                            PopUntilLParen(output, stack);
                            prevWasValue = true;
                            break;

                        case TokenKind.Comma:
                            PopUntilLParen(output, stack, keepLParen: true);
                            prevWasValue = false;
                            break;

                        case TokenKind.Plus:
                        case TokenKind.Minus:
                        case TokenKind.Star:
                        case TokenKind.Slash:
                            {
                                var unary = (t.Kind == TokenKind.Minus || t.Kind == TokenKind.Plus) && !prevWasValue;
                                PushOperator(output, stack, t.Kind, unary);
                                prevWasValue = false;
                                break;
                            }

                        case TokenKind.End:
                            i = tokens.Count;
                            break;

                        default:
                            throw new InvalidOperationException($"Unsupported token: {t.Kind}");
                    }
                }

                while (stack.Count > 0)
                {
                    var top = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);

                    if (top.Kind == StackItemKind.LParen)
                    {
                        throw new InvalidOperationException("Mismatched parentheses");
                    }

                    EmitStackItem(output, top);
                }

                var depIds = deps.Count == 0 ? Array.Empty<AttributeId>() : new AttributeId[deps.Count];
                if (deps.Count > 0)
                {
                    var idx = 0;
                    foreach (var id in deps)
                    {
                        depIds[idx++] = new AttributeId(id);
                    }
                }

                return new Parsed(output.ToArray(), depIds);
            }

            private static void PushOperator(List<Instruction> output, List<StackItem> stack, TokenKind op, bool unary)
            {
                var prec = Precedence(op, unary);

                while (stack.Count > 0)
                {
                    var top = stack[stack.Count - 1];
                    if (top.Kind == StackItemKind.LParen) break;

                    if (top.Kind == StackItemKind.Func)
                    {
                        EmitStackItem(output, top);
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }

                    var topPrec = Precedence(top.Op, top.Unary);
                    if (topPrec >= prec)
                    {
                        EmitStackItem(output, top);
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }

                    break;
                }

                stack.Add(StackItem.O(op, unary));
            }

            private static int Precedence(TokenKind op, bool unary)
            {
                if (unary) return 3;
                return op switch
                {
                    TokenKind.Star => 2,
                    TokenKind.Slash => 2,
                    TokenKind.Plus => 1,
                    TokenKind.Minus => 1,
                    _ => 0,
                };
            }

            private static void PopUntilLParen(List<Instruction> output, List<StackItem> stack, bool keepLParen = false)
            {
                while (stack.Count > 0)
                {
                    var top = stack[stack.Count - 1];
                    if (top.Kind == StackItemKind.LParen)
                    {
                        if (!keepLParen)
                        {
                            stack.RemoveAt(stack.Count - 1);

                            if (stack.Count > 0 && stack[stack.Count - 1].Kind == StackItemKind.Func)
                            {
                                var f = stack[stack.Count - 1];
                                stack.RemoveAt(stack.Count - 1);
                                EmitStackItem(output, f);
                            }
                        }
                        return;
                    }

                    stack.RemoveAt(stack.Count - 1);
                    EmitStackItem(output, top);
                }

                throw new InvalidOperationException("Mismatched parentheses");
            }

            private static void EmitStackItem(List<Instruction> output, StackItem item)
            {
                if (item.Kind == StackItemKind.Op)
                {
                    if (item.Unary)
                    {
                        if (item.Op == TokenKind.Minus)
                        {
                            output.Add(Instruction.Op1(OpCode.Neg));
                            return;
                        }

                        if (item.Op == TokenKind.Plus)
                        {
                            return;
                        }
                    }

                    switch (item.Op)
                    {
                        case TokenKind.Plus: output.Add(Instruction.Op1(OpCode.Add)); return;
                        case TokenKind.Minus: output.Add(Instruction.Op1(OpCode.Sub)); return;
                        case TokenKind.Star: output.Add(Instruction.Op1(OpCode.Mul)); return;
                        case TokenKind.Slash: output.Add(Instruction.Op1(OpCode.Div)); return;
                    }

                    throw new InvalidOperationException($"Unsupported operator: {item.Op}");
                }

                if (item.Kind == StackItemKind.Func)
                {
                    if (string.Equals(item.Func, "abs", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Add(Instruction.Op1(OpCode.FuncAbs));
                        return;
                    }
                    if (string.Equals(item.Func, "min", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Add(Instruction.Op1(OpCode.FuncMin));
                        return;
                    }
                    if (string.Equals(item.Func, "max", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Add(Instruction.Op1(OpCode.FuncMax));
                        return;
                    }
                    if (string.Equals(item.Func, "clamp", StringComparison.OrdinalIgnoreCase))
                    {
                        output.Add(Instruction.Op1(OpCode.FuncClamp));
                        return;
                    }

                    throw new InvalidOperationException($"Unknown function: {item.Func}");
                }

                throw new InvalidOperationException($"Unexpected stack item: {item.Kind}");
            }

            private static bool TryBuiltin(string ident, out BuiltinVar v)
            {
                v = default;
                if (string.IsNullOrEmpty(ident)) return false;

                if (string.Equals(ident, "base", StringComparison.OrdinalIgnoreCase)) { v = BuiltinVar.Base; return true; }
                if (string.Equals(ident, "add", StringComparison.OrdinalIgnoreCase)) { v = BuiltinVar.Add; return true; }
                if (string.Equals(ident, "mul", StringComparison.OrdinalIgnoreCase)) { v = BuiltinVar.Mul; return true; }
                if (string.Equals(ident, "override", StringComparison.OrdinalIgnoreCase)) { v = BuiltinVar.Override; return true; }
                if (string.Equals(ident, "hasOverride", StringComparison.OrdinalIgnoreCase)) { v = BuiltinVar.HasOverride; return true; }

                return false;
            }

            private static List<Token> Tokenize(string expr)
            {
                var list = new List<Token>(64);
                var i = 0;
                while (i < expr.Length)
                {
                    var c = expr[i];
                    if (char.IsWhiteSpace(c)) { i++; continue; }

                    if (c == '+') { list.Add(new Token(TokenKind.Plus, "+", 0f)); i++; continue; }
                    if (c == '-') { list.Add(new Token(TokenKind.Minus, "-", 0f)); i++; continue; }
                    if (c == '*') { list.Add(new Token(TokenKind.Star, "*", 0f)); i++; continue; }
                    if (c == '/') { list.Add(new Token(TokenKind.Slash, "/", 0f)); i++; continue; }
                    if (c == '(') { list.Add(new Token(TokenKind.LParen, "(", 0f)); i++; continue; }
                    if (c == ')') { list.Add(new Token(TokenKind.RParen, ")", 0f)); i++; continue; }
                    if (c == ',') { list.Add(new Token(TokenKind.Comma, ",", 0f)); i++; continue; }

                    if (char.IsDigit(c) || c == '.')
                    {
                        var start = i;
                        i++;
                        while (i < expr.Length)
                        {
                            var cc = expr[i];
                            if (char.IsDigit(cc) || cc == '.') { i++; continue; }
                            break;
                        }
                        var s = expr.Substring(start, i - start);
                        if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                        {
                            throw new InvalidOperationException($"Invalid number: {s}");
                        }
                        list.Add(new Token(TokenKind.Number, s, f));
                        continue;
                    }

                    if (char.IsLetter(c) || c == '_')
                    {
                        var start = i;
                        i++;
                        while (i < expr.Length)
                        {
                            var cc = expr[i];
                            if (char.IsLetterOrDigit(cc) || cc == '_' || cc == '.') { i++; continue; }
                            break;
                        }
                        var s = expr.Substring(start, i - start);
                        list.Add(new Token(TokenKind.Ident, s, 0f));
                        continue;
                    }

                    throw new InvalidOperationException($"Invalid character '{c}' in expression");
                }

                list.Add(new Token(TokenKind.End, string.Empty, 0f));
                return list;
            }
        }
    }
}
