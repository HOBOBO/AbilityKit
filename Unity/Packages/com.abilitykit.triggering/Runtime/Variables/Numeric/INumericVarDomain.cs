using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Triggering.Variables.Numeric
{
    public interface INumericVarDomain
    {
        string DomainId { get; }

        bool TryGet<TCtx>(in ExecCtx<TCtx> ctx, string key, out double value);

        bool TrySet<TCtx>(in ExecCtx<TCtx> ctx, string key, double value);
    }
}
