using System;

namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
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
            r.Register(new Exp());
            r.Register(new Log());
            r.Register(new Log10());
            r.Register(new Sin());
            r.Register(new Cos());
            r.Register(new Tan());
            r.Register(new Min());
            r.Register(new Max());
            r.Register(new Clamp());
            r.Register(new Clamp01());
            r.Register(new Lerp());
            r.Register(new Atan2());
            r.Register(new Cbrt());
            r.Register(new Log2());
            r.Register(new Trunc());
            r.Register(new Fract());
            r.Register(new Mod());
            r.Register(new Percent());
            return r;
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
                result = Math.Sqrt(args[0]);
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
                result = Math.Pow(args[0], args[1]);
                return true;
            }
        }

        private sealed class Exp : INumericRpnFunction
        {
            public string Name => "exp";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Exp(args[0]);
                return true;
            }
        }

        private sealed class Log : INumericRpnFunction
        {
            public string Name => "log";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (args[0] <= 0d) return false;
                result = Math.Log(args[0]);
                return true;
            }
        }

        private sealed class Log10 : INumericRpnFunction
        {
            public string Name => "log10";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (args[0] <= 0d) return false;
                result = Math.Log10(args[0]);
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
                result = Math.Sin(args[0]);
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
                result = Math.Cos(args[0]);
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
                result = Math.Tan(args[0]);
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
                result = Math.Atan2(args[0], args[1]);
                return true;
            }
        }

        private sealed class Cbrt : INumericRpnFunction
        {
            public string Name => "cbrt";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                result = Math.Cbrt(args[0]);
                return true;
            }
        }

        private sealed class Log2 : INumericRpnFunction
        {
            public string Name => "log2";
            public int ArgCount => 1;

            public bool TryInvoke(double[] args, out double result)
            {
                result = 0d;
                if (args == null || args.Length != 1) return false;
                if (args[0] <= 0d) return false;
                result = Math.Log(args[0], 2d);
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
