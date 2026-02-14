using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Explain
{
    [Serializable]
    public sealed class ExplainResolveResult
    {
        public ExplainForest Forest;

        public ExplainTimeline Timeline;

        public List<ExplainIssue> Issues = new List<ExplainIssue>();

        public long ElapsedMs;

        public bool CacheHit;

        public bool Partial;

        public string Debug;

        public string Exception;

        public static ExplainResolveResult FromForest(ExplainForest forest)
        {
            return new ExplainResolveResult { Forest = forest };
        }
    }
}
