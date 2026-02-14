using System;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainResolveRequest
    {
        public PipelineItemKey Key;

        public ExplainResolveContext Context;

        public ExplainResolveOptions Options;

        public static ExplainResolveRequest For(in PipelineItemKey key, ExplainResolveContext context = null, ExplainResolveOptions options = null)
        {
            return new ExplainResolveRequest
            {
                Key = key,
                Context = context,
                Options = options
            };
        }
    }
}
