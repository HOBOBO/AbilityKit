using System;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainExpandRequest
    {
        public PipelineItemKey RootKey;

        public ExplainResolveContext Context;

        public ExplainResolveOptions Options;

        public static ExplainExpandRequest For(in PipelineItemKey rootKey, ExplainResolveContext context = null, ExplainResolveOptions options = null)
        {
            return new ExplainExpandRequest
            {
                RootKey = rootKey,
                Context = context,
                Options = options
            };
        }
    }
}
