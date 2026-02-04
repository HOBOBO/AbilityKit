using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class RpnNumericExprRuntime
    {
        private readonly RpnNumericExprPlan _plan;
        private RpnNumericNode[] _cached;

        public RpnNumericExprRuntime(RpnNumericExprPlan plan)
        {
            _plan = plan;
        }

        public double Eval<TArgs, TCtx>(in TArgs args, in ExecCtx<TCtx> ctx,
            Func<string, int> payloadFieldIdResolver = null,
            Func<string, int> blackboardDomainIdResolver = null,
            Func<string, int> blackboardKeyIdResolver = null)
        {
            var nodes = _plan.Nodes;
            if (nodes == null)
            {
                if (_cached == null)
                {
                    if (!string.Equals(_plan.ExprLang, RpnNumericExprParser.LangRpnV1, StringComparison.Ordinal))
                        throw new InvalidOperationException("Unsupported expr lang: " + _plan.ExprLang);
                    _cached = RpnNumericExprParser.Parse(_plan.ExprText, payloadFieldIdResolver, blackboardDomainIdResolver, blackboardKeyIdResolver);
                }

                nodes = _cached;
            }

            return RpnNumericExprEval.Eval<TArgs, TCtx>(nodes, in args, in ctx);
        }
    }
}
