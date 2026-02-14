using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainForest
    {
        public List<ExplainTreeRoot> Roots = new List<ExplainTreeRoot>();
        public List<ExplainTreeDiscovery> Discovered = new List<ExplainTreeDiscovery>();
    }

    [Serializable]
    public sealed class ExplainTreeRoot
    {
        public string Kind;
        public PipelineItemKey Key;
        public string Title;
        public ExplainNode Root;
    }

    [Serializable]
    public sealed class ExplainTreeDiscovery
    {
        public string Kind;
        public PipelineItemKey Key;
        public string Title;
        public int RefCount;
    }
}
