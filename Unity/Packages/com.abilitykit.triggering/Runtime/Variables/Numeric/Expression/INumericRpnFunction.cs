namespace AbilityKit.Triggering.Variables.Numeric.Expression
{
    public interface INumericRpnFunction
    {
        string Name { get; }
        int ArgCount { get; }
        bool TryInvoke(double[] args, out double result);
    }
}
