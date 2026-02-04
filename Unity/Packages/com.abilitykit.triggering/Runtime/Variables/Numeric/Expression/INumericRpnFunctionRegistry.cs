namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public interface INumericRpnFunctionRegistry
    {
        bool TryGet(string name, out INumericRpnFunction function);
    }
}
