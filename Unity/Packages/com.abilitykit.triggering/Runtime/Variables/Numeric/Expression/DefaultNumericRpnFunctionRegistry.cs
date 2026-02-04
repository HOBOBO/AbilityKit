namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public sealed class DefaultNumericRpnFunctionRegistry : INumericRpnFunctionRegistry
    {
        public static readonly DefaultNumericRpnFunctionRegistry Instance = new DefaultNumericRpnFunctionRegistry();

        private readonly NumericRpnFunctionRegistry _inner;

        private DefaultNumericRpnFunctionRegistry()
        {
            _inner = DefaultNumericRpnFunctions.CreateRegistry();
        }

        public bool TryGet(string name, out INumericRpnFunction function)
        {
            return _inner.TryGet(name, out function);
        }
    }
}
