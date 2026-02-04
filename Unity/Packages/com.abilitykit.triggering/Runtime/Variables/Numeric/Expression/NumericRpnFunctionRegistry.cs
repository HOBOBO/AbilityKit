using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public sealed class NumericRpnFunctionRegistry : INumericRpnFunctionRegistry
    {
        private readonly Dictionary<string, INumericRpnFunction> _functions;

        public NumericRpnFunctionRegistry()
        {
            _functions = new Dictionary<string, INumericRpnFunction>(StringComparer.Ordinal);
        }

        public bool TryGet(string name, out INumericRpnFunction function)
        {
            if (name == null)
            {
                function = null;
                return false;
            }

            return _functions.TryGetValue(name, out function);
        }

        public void Register(INumericRpnFunction function)
        {
            if (function == null) throw new ArgumentNullException(nameof(function));
            if (string.IsNullOrEmpty(function.Name)) throw new ArgumentException("function.Name is null or empty", nameof(function));

            _functions[function.Name] = function;
        }
    }
}
