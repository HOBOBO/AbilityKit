using System;
using System.Collections.Generic;
using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    [Serializable]
    public sealed class ExplainEntityListGroup
    {
        public string Title;
        public List<PipelineItemKey> Items = new List<PipelineItemKey>();
    }
}
