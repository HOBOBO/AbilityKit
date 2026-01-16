namespace AbilityKit.Ability.Triggering.Variables.Numeric.Expression
{
    public interface INumericRpnFunctionRegistry
    {
        bool TryGet(string name, out INumericRpnFunction function);
    }
}
