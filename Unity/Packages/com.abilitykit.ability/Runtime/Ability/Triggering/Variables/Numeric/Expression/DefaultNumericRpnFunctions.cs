using System;
using AbilityKit.Deterministic;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    /// <summary>
    /// 数值表达式的默认 RPN 函数库。表达式结果直接进入帧同步模拟，
    /// 因此全部漂移敏感函数（开方/三角/乘方）走 AbilityKit.Deterministic 的整数实现，
    /// 求值域为 Q32.32（±2.1e9、分辨率 ~2.3e-10）。
    /// exp/log/log10/log2/cbrt 没有确定性实现且无配置使用，已从注册表移除——
    /// 如未来需要，必须先在 Deterministic 包补整数实现再开放。
    /// </summary>
    public static class DefaultNumericRpnFunctions
    {
        public static NumericRpnFunctionRegistry CreateRegistry()
        {
            var r = new NumericRpnFunctionRegistry();
            r.Register(new Abs());
            r.Register(new Sign());
            r.Register(new Floor());
            r.Register(new Ceil());
            r.Register(new Round());
            r.Register(new Sqrt());
            r.Register(new Pow());
            r.Register(new Sin());
            r.Register(new Cos());
            r.Register(new Tan());
            r.Register(new Min());
            r.Register(new Max());
            r.Register(new Clamp());
            r.Register(new Clamp01());
            r.Register(new Lerp());
            r.Register(new Atan2());
            r.Register(new Trunc());
            r.Register(new Fract());
            r.Register(new Mod());
            r.Register(new Percent());
            return r;
        }

        private static bool TryToFixed(double value, out Fixed64 fixedValue)
        {
            fixedValue = Fixed64.Zero;
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            if (value > 2147483647d || value < -2147483648d) return false;
            fixedValue = Fixed64.FromDouble(value);
            return true;
        }

        private sealed class Abs : INumericRpnFunction
        {
            public string Name => "abs";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Abs(args[0]);
                return true;
            }
        }

        private sealed class Sign : INumericRpnFunction
        {
            public string Name => "sign";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Sign(args[0]);
                return true;
            }
        }

        private sealed class Floor : INumericRpnFunction
        {
            public string Name => "floor";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Floor(args[0]);
                return true;
            }
        }

        private sealed class Ceil : INumericRpnFunction
        {
            public string Name => "ceil";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Ceiling(args[0]);
                return true;
            }
        }

        private sealed class Round : INumericRpnFunction
        {
            public string Name => "round";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Round(args[0]);
                return true;
            }
        }

        private sealed class Sqrt : INumericRpnFunction
        {
            public string Name => "sqrt";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (args[0] < 0d) return false;
                if (!TryToFixed(args[0], out var v)) return false;
                result = DeterministicMath.Sqrt(v).ToDouble();
                return true;
            }
        }

        private sealed class Pow : INumericRpnFunction
        {
            public string Name => "pow";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;

                // 仅支持整数指数（定点平方-乘）。分数指数无确定性实现，拒绝求值。
                var exponent = args[1];
                if (Math.Floor(exponent) != exponent || Math.Abs(exponent) > 62d) return false;

                if (!TryToFixed(args[0], out var basis)) return false;
                var negativeBase = basis < Fixed64.Zero;
                var magnitude = negativeBase ? -basis : basis;
                var power = (int)exponent;
                var isOdd = power % 2 != 0;

                var acc = Fixed64.One;
                var factor = magnitude;
                var e = power < 0 ? -power : power;
                while (e > 0)
                {
                    if ((e & 1) == 1) acc *= factor;
                    if (e > 1) factor *= factor;
                    e >>= 1;
                }

                if (power < 0)
                {
                    if (acc == Fixed64.Zero) return false;
                    acc = Fixed64.One / acc;
                }

                result = (negativeBase && isOdd ? -acc : acc).ToDouble();
                return true;
            }
        }

        private sealed class Sin : INumericRpnFunction
        {
            public string Name => "sin";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (!TryToFixed(args[0], out var v)) return false;
                result = DeterministicMath.Sin(v).ToDouble();
                return true;
            }
        }

        private sealed class Cos : INumericRpnFunction
        {
            public string Name => "cos";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (!TryToFixed(args[0], out var v)) return false;
                result = DeterministicMath.Cos(v).ToDouble();
                return true;
            }
        }

        private sealed class Tan : INumericRpnFunction
        {
            public string Name => "tan";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (!TryToFixed(args[0], out var v)) return false;
                result = DeterministicMath.Tan(v).ToDouble();
                return true;
            }
        }

        private sealed class Min : INumericRpnFunction
        {
            public string Name => "min";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;
                result = Math.Min(args[0], args[1]);
                return true;
            }
        }

        private sealed class Max : INumericRpnFunction
        {
            public string Name => "max";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;
                result = Math.Max(args[0], args[1]);
                return true;
            }
        }

        private sealed class Clamp : INumericRpnFunction
        {
            public string Name => "clamp";
            public int ArgCount => 3;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 3) return false;

                var v = args[0];
                var min = args[1];
                var max = args[2];

                if (min > max)
                {
                    var t = min;
                    min = max;
                    max = t;
                }

                if (v < min) v = min;
                if (v > max) v = max;

                result = v;
                return true;
            }
        }

        private sealed class Clamp01 : INumericRpnFunction
        {
            public string Name => "clamp01";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;

                var v = args[0];
                if (v < 0d) v = 0d;
                if (v > 1d) v = 1d;
                result = v;
                return true;
            }
        }

        private sealed class Lerp : INumericRpnFunction
        {
            public string Name => "lerp";
            public int ArgCount => 3;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 3) return false;

                var a = args[0];
                var b = args[1];
                var t = args[2];
                result = a + (b - a) * t;
                return true;
            }
        }

        #region 补充函数

        private sealed class Atan2 : INumericRpnFunction
        {
            public string Name => "atan2";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;
                if (!TryToFixed(args[0], out var y) || !TryToFixed(args[1], out var x)) return false;
                if (y == Fixed64.Zero && x == Fixed64.Zero) return false;
                result = DeterministicMath.Atan2(y, x).ToDouble();
                return true;
            }
        }

        private sealed class Trunc : INumericRpnFunction
        {
            public string Name => "trunc";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Truncate(args[0]);
                return true;
            }
        }

        private sealed class Fract : INumericRpnFunction
        {
            public string Name => "fract";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = args[0] - Math.Truncate(args[0]);
                return true;
            }
        }

        private sealed class Mod : INumericRpnFunction
        {
            public string Name => "mod";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;
                if (args[1] == 0d) return false;
                result = args[0] % args[1];
                return true;
            }
        }

        private sealed class Percent : INumericRpnFunction
        {
            public string Name => "percent";
            public int ArgCount => 2;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 2) return false;
                if (args[1] == 0d) return false;
                result = args[0] / args[1];
                return true;
            }
        }

        #endregion
    }
}
