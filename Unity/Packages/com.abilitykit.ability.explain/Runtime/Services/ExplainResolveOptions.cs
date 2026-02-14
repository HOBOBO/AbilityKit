using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainResolveOptions
    {
        public bool IncludeDiscovered = true;

        public int MaxDepth = 0;

        public Dictionary<string, string> Extra;

        public static ExplainResolveOptions Default => new ExplainResolveOptions();
    }
}
