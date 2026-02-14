using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainResolveContext
    {
        public PipelineItemKey Key;

        public Dictionary<string, string> Values;

        public static ExplainResolveContext For(in PipelineItemKey key)
        {
            return new ExplainResolveContext { Key = key };
        }
    }
}
