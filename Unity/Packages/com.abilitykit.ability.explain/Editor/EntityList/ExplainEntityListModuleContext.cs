using System;

namespace AbilityKit.Ability.Explain.Editor
{
    public readonly struct ExplainEntityListModuleContext
    {
        public readonly Action RequestRefresh;

        public ExplainEntityListModuleContext(Action requestRefresh)
        {
            RequestRefresh = requestRefresh;
        }
    }
}
