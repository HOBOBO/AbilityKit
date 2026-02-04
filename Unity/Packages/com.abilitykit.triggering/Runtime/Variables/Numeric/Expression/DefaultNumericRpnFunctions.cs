using System;

namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public static class DefaultNumericRpnFunctions
    {
        public static NumericRpnFunctionRegistry CreateRegistry()
        {
            var r = new NumericRpnFunctionRegistry();
            r.Register(new Abs());
            r.Register(new Min());
            r.Register(new Max());
            r.Register(new Clamp());
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
                if (min > max) return false;
                if (v < min) v = min;
                if (v > max) v = max;
                result = v;
                return true;
            }
        }
    }
}
