using System;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public sealed class RpnIntExprRuntime
    {
        private readonly RpnIntExprPlan _plan;
        private RpnIntNode[] _cached;

        public RpnIntExprRuntime(RpnIntExprPlan plan)
        {
            _plan = plan;
        }

        public int Eval<TArgs>(in TArgs args, in ExecCtx ctx,
            Func<string, int> payloadFieldIdResolver = null,
            Func<string, int> blackboardDomainIdResolver = null,
            Func<string, int> blackboardKeyIdResolver = null)
        {
            var nodes = _plan.Nodes;
            if (nodes == null)
            {
                if (_cached == null)
                {
                    if (!string.Equals(_plan.ExprLang, RpnIntExprParser.LangRpnV1, StringComparison.Ordinal))
                        throw new InvalidOperationException("Unsupported expr lang: " + _plan.ExprLang);
                    _cached = RpnIntExprParser.Parse(_plan.ExprText, payloadFieldIdResolver, blackboardDomainIdResolver, blackboardKeyIdResolver);
                }

                nodes = _cached;
            }

            return RpnIntExprEval.Eval(nodes, in args, in ctx);
        }
    }
}
